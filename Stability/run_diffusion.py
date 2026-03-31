import argparse
from pathlib import Path


def _parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description=(
            "Run Stable Diffusion 3.5 Large fully offline using local files.\n"
            "You must have BOTH:\n"
            "- the base SD3.5 diffusers folder (VAE/encoders/tokenizers/scheduler/etc)\n"
            "- the transformer single-file checkpoint (sd3.5_large.safetensors)\n"
            "This script does not download anything and will not use the Hugging Face Hub."
        )
    )
    p.add_argument(
        "--base",
        default=None,
        help="Local folder containing the SD3.5 diffusers files. Default: ./sd3.5_base next to this script.",
    )
    p.add_argument(
        "--transformer",
        default=None,
        help="Path to local sd3.5_large.safetensors. Default: ./sd3.5_large.safetensors next to this script.",
    )
    p.add_argument("--prompt", required=True, help="Text prompt.")
    p.add_argument("--negative", default=None, help="Negative prompt (optional).")
    p.add_argument("--out", default="out.png", help="Output image path.")
    p.add_argument("--steps", type=int, default=40, help="Inference steps.")
    p.add_argument("--guidance", type=float, default=4.5, help="CFG guidance scale.")
    p.add_argument("--seed", type=int, default=None, help="Random seed (optional).")
    p.add_argument("--width", type=int, default=1024, help="Output width.")
    p.add_argument("--height", type=int, default=1024, help="Output height.")
    p.add_argument(
        "--device",
        default=None,
        choices=[None, "cuda", "cpu", "mps"],
        help="Force device. Default: auto.",
    )
    p.add_argument(
        "--precision",
        default="auto",
        choices=["auto", "bf16", "fp16", "fp32"],
        help="Torch dtype. Default: bf16 on CUDA (if supported), else fp32.",
    )
    p.add_argument(
        "--cpu-offload",
        action="store_true",
        help="Enable model CPU offload (CUDA only). Helps fit on smaller VRAM.",
    )
    return p.parse_args()


def main() -> int:
    args = _parse_args()

    # Lazy imports so argparse --help works without deps installed.
    import torch
    from diffusers import SD3Transformer2DModel, StableDiffusion3Pipeline

    if args.device is None:
        if torch.cuda.is_available():
            device = "cuda"
        elif getattr(torch.backends, "mps", None) is not None and torch.backends.mps.is_available():
            device = "mps"
        else:
            device = "cpu"
    else:
        device = args.device

    if args.precision == "auto":
        if device == "cuda" and torch.cuda.is_available():
            # Prefer bf16 on modern NVIDIA GPUs, otherwise fp16.
            dtype = torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16
        else:
            dtype = torch.float32
    else:
        dtype = {
            "bf16": torch.bfloat16,
            "fp16": torch.float16,
            "fp32": torch.float32,
        }[args.precision]

    script_dir = Path(__file__).resolve().parent
    base_dir = Path(args.base).expanduser().resolve() if args.base else (script_dir / "sd3.5_base")
    transformer_path = (
        Path(args.transformer).expanduser().resolve()
        if args.transformer
        else (script_dir / "sd3.5_large.safetensors")
    )

    if not base_dir.exists() or not base_dir.is_dir():
        raise SystemExit(
            f"--base folder not found: {base_dir}\n"
            "Put the SD3.5 diffusers model files in a local folder and pass it with --base.\n"
            "Example expected contents include subfolders like: transformer/, vae/, text_encoder/, text_encoder_2/, tokenizer/, scheduler/."
        )
    if not transformer_path.exists() or not transformer_path.is_file():
        raise SystemExit(f"--transformer file not found: {transformer_path}")

    transformer = SD3Transformer2DModel.from_single_file(
        str(transformer_path),
        torch_dtype=dtype,
    )

    pipe = StableDiffusion3Pipeline.from_pretrained(
        str(base_dir),
        transformer=transformer,
        torch_dtype=dtype,
        local_files_only=True,
    )

    if device == "cuda" and args.cpu_offload:
        pipe.enable_model_cpu_offload()
    else:
        pipe = pipe.to(device)

    generator = None
    if args.seed is not None:
        generator = torch.Generator(device=device).manual_seed(args.seed)

    image = pipe(
        prompt=args.prompt,
        negative_prompt=args.negative,
        num_inference_steps=args.steps,
        guidance_scale=args.guidance,
        width=args.width,
        height=args.height,
        generator=generator,
    ).images[0]

    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(out_path)
    print(f"Saved: {out_path.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

