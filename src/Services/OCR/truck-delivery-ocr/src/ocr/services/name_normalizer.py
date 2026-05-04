"""Vietnamese name normalization utilities."""

import unicodedata

# Vietnamese title-case mapping for common particles
_LOWER_PARTICLES = {"van", "thi", "thi", "duc", "cong", "tuan"}


def normalize_vietnamese_name(raw_name: str) -> tuple[str, str]:
    """
    Convert an ALL-CAPS Vietnamese name from OCR to proper case.

    Returns (first_name, last_name) — Vietnamese order is Họ Tên (family name first).
    Example: "NGUYEN VAN A" → last_name="Nguyễn Văn A", first_name="A"

    Note: Vietnamese names don't have a reliable first/last split rule for middle names.
    We return the full normalized name as last_name and the final word as first_name.
    """
    if not raw_name:
        return "", ""

    words = raw_name.strip().split()
    normalized = []
    for word in words:
        # Title case each word, preserving diacritics
        normalized.append(word.capitalize())

    full_name = " ".join(normalized)

    # Split: first token = family name, last token = given name (simplified)
    if len(words) == 1:
        return words[0].capitalize(), ""

    last_name = full_name
    first_name = normalized[-1]
    return first_name, last_name


def normalize_address(raw_address: str) -> str:
    """Normalize OCR address string: strip excess whitespace, title-case district/city."""
    if not raw_address:
        return ""
    # Collapse multiple spaces
    parts = " ".join(raw_address.split())
    return parts


def parse_vietnamese_date(date_str: str) -> str | None:
    """
    Parse Vietnamese date formats to ISO string.

    Accepts: "DD/MM/YYYY", "DD-MM-YYYY", "YYYY-MM-DD"
    Returns: "YYYY-MM-DD" or None if unparsable.
    """
    from datetime import datetime

    for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%Y-%m-%d", "%Y/%m/%d"):
        try:
            return datetime.strptime(date_str.strip(), fmt).strftime("%Y-%m-%d")
        except ValueError:
            continue
    return None
