import argparse
import os
import shutil
from pathlib import Path


def _parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description=(
            "Prefetch Stable Diffusion 3.5 Large assets for offline inference.\n"
            "This downloads the full diffusers repo snapshot to a local folder so cluster nodes can run with --local_files_only.\n"
            "NOTE: stabilityai/stable-diffusion-3.5-large is gated, so you must authenticate for the download step."
        )
    )
    p.add_argument(
        "--repo",
        default="stabilityai/stable-diffusion-3.5-large",
        help="Hugging Face repo id to download.",
    )
    p.add_argument(
        "--base-out",
        default="sd3.5_base",
        help="Output folder to store the diffusers repo files (pass this to run_diffusion.py --base).",
    )
    p.add_argument(
        "--transformer-out",
        default="sd3.5_large.safetensors",
        help="Output path for the transformer single-file checkpoint (pass this to run_diffusion.py --transformer).",
    )
    p.add_argument(
        "--revision",
        default=None,
        help="Optional repo revision/commit hash/tag.",
    )
    p.add_argument(
        "--token",
        default=None,
        help="HF token (or set HF_TOKEN / HUGGINGFACE_HUB_TOKEN). Required for gated models.",
    )
    p.add_argument(
        "--clean",
        action="store_true",
        help="If set, deletes base-out before downloading.",
    )
    return p.parse_args()


def main() -> int:
    args = _parse_args()

    from huggingface_hub import snapshot_download

    token = (
        args.token
        or os.environ.get("HF_TOKEN")
        or os.environ.get("HUGGINGFACE_HUB_TOKEN")
        or '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'
    )

    base_out = Path(args.base_out).expanduser().resolve()
    transformer_out = Path(args.transformer_out).expanduser().resolve()

    if args.clean and base_out.exists():
        shutil.rmtree(base_out)
    base_out.mkdir(parents=True, exist_ok=True)
    transformer_out.parent.mkdir(parents=True, exist_ok=True)

    # Download *all* files needed for offline inference (weights, configs, tokenizers, scheduler, etc.).
    # We ignore obviously non-essential hub files to save a bit of space.
    print(f"Downloading {args.repo} -> {base_out}")
    snapshot_dir = snapshot_download(
        repo_id=args.repo,
        revision=args.revision,
        local_dir=str(base_out),
        local_dir_use_symlinks=False,
        token=token,
        ignore_patterns=[
            "*.md",
            "*.jpg",
            "*.jpeg",
            "*.png",
            "*.gif",
            "*.webp",
            "*.mp4",
            "*.mov",
            "*.pdf",
        ],
    )
    print(f"Snapshot downloaded to: {snapshot_dir}")

    # Copy the single-file transformer checkpoint to a stable path.
    # The filename in the repo is typically: sd3.5_large.safetensors
    src_transformer = base_out / "sd3.5_large.safetensors"
    if not src_transformer.exists():
        raise SystemExit(
            "Could not find sd3.5_large.safetensors inside the downloaded repo.\n"
            f"Looked for: {src_transformer}\n"
            "If the repo layout changed, locate the file and adjust this script."
        )

    shutil.copy2(src_transformer, transformer_out)
    print(f"Copied transformer checkpoint -> {transformer_out}")

    print("\nOffline run example (on cluster node):")
    print(
        f'  python run_diffusion.py --base "{base_out}" --transformer "{transformer_out}" --prompt "a cat holding a sign that says hello world" --out out.png'
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

