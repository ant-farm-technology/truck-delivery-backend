from ocr.services.name_normalizer import normalize_vietnamese_name, parse_vietnamese_date


def test_normalize_all_caps_name():
    first, last = normalize_vietnamese_name("NGUYEN VAN A")
    assert first == "A"
    assert last == "Nguyen Van A"


def test_normalize_single_word_name():
    first, last = normalize_vietnamese_name("NGUYEN")
    assert first == "Nguyen"
    assert last == ""


def test_normalize_empty_name():
    first, last = normalize_vietnamese_name("")
    assert first == ""
    assert last == ""


def test_parse_date_slash_format():
    result = parse_vietnamese_date("15/05/1990")
    assert result == "1990-05-15"


def test_parse_date_dash_format():
    result = parse_vietnamese_date("15-05-1990")
    assert result == "1990-05-15"


def test_parse_date_iso_format():
    result = parse_vietnamese_date("1990-05-15")
    assert result == "1990-05-15"


def test_parse_date_invalid():
    result = parse_vietnamese_date("not-a-date")
    assert result is None
