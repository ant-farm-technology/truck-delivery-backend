import io

import httpx
from PIL import Image

from ocr.config import settings


async def load_image_from_url(url: str) -> Image.Image:
    """Download image from URL and return as PIL Image."""
    async with httpx.AsyncClient(timeout=settings.http_timeout_seconds) as client:
        response = await client.get(url)
        response.raise_for_status()
    return Image.open(io.BytesIO(response.content)).convert("RGB")


def image_to_numpy(img: Image.Image):
    """Convert PIL Image to numpy array for PaddleOCR."""
    import numpy as np
    return np.array(img)
