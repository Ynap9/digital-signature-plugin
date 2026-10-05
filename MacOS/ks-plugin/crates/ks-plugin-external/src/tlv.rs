use crate::card::{CardError, CardResult};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Tlv<'a> {
    pub tag: u32,
    pub value: &'a [u8],
}

pub fn children(mut bytes: &[u8]) -> CardResult<Vec<Tlv<'_>>> {
    let mut items = Vec::new();
    while let Some((&first, _)) = bytes.split_first() {
        if first == 0x00 || first == 0xFF {
            break;
        }
        let (item, rest) = read_one(bytes)?;
        items.push(item);
        bytes = rest;
    }
    Ok(items)
}

pub fn padded_length(bytes: &[u8]) -> Option<usize> {
    let mut offset = 0;
    loop {
        match bytes.get(offset) {
            None => return None,
            Some(0x00 | 0xFF) => return Some(offset),
            Some(_) => offset += item_length(&bytes[offset..])?,
        }
    }
}

pub fn single_item_length(bytes: &[u8]) -> Option<usize> {
    item_length(bytes).filter(|length| *length <= bytes.len())
}

pub fn find(bytes: &[u8], tag: u32) -> CardResult<Option<&[u8]>> {
    Ok(children(bytes)?
        .into_iter()
        .find(|item| item.tag == tag)
        .map(|item| item.value))
}

pub fn require(bytes: &[u8], tag: u32) -> CardResult<&[u8]> {
    find(bytes, tag)?.ok_or(CardError::Malformed("thiếu trường TLV bắt buộc"))
}

pub fn unsigned(value: &[u8]) -> CardResult<u32> {
    if value.is_empty() || value.len() > 5 || (value.len() == 5 && value[0] != 0) {
        return Err(CardError::Malformed("số nguyên TLV sai độ dài"));
    }
    Ok(value
        .iter()
        .fold(0u32, |acc, byte| (acc << 8) | u32::from(*byte)))
}

pub fn text(value: &[u8]) -> String {
    String::from_utf8_lossy(value)
        .trim_matches([' ', '\0'])
        .to_owned()
}

fn item_length(bytes: &[u8]) -> Option<usize> {
    let (_, after_tag) = read_tag(bytes).ok()?;
    let (length, after_length) = read_length(after_tag).ok()?;
    Some(bytes.len() - after_length.len() + length)
}

fn read_one(bytes: &[u8]) -> CardResult<(Tlv<'_>, &[u8])> {
    let (tag, after_tag) = read_tag(bytes)?;
    let (length, after_length) = read_length(after_tag)?;
    if after_length.len() < length {
        return Err(CardError::Malformed("TLV khai độ dài vượt dữ liệu"));
    }
    let (value, rest) = after_length.split_at(length);
    Ok((Tlv { tag, value }, rest))
}

fn read_tag(bytes: &[u8]) -> CardResult<(u32, &[u8])> {
    let (&first, mut rest) = bytes
        .split_first()
        .ok_or(CardError::Malformed("TLV thiếu tag"))?;
    let mut tag = u32::from(first);
    if first & 0x1F == 0x1F {
        loop {
            let (&next, after) = rest
                .split_first()
                .ok_or(CardError::Malformed("tag TLV bị cắt"))?;
            if tag > 0x00FF_FFFF {
                return Err(CardError::Malformed("tag TLV quá dài"));
            }
            tag = (tag << 8) | u32::from(next);
            rest = after;
            if next & 0x80 == 0 {
                break;
            }
        }
    }
    Ok((tag, rest))
}

fn read_length(bytes: &[u8]) -> CardResult<(usize, &[u8])> {
    match bytes {
        [short, rest @ ..] if *short < 0x80 => Ok((usize::from(*short), rest)),
        [0x81, length, rest @ ..] => Ok((usize::from(*length), rest)),
        [0x82, high, low, rest @ ..] => Ok((usize::from(u16::from_be_bytes([*high, *low])), rest)),
        _ => Err(CardError::Malformed("độ dài TLV không hỗ trợ")),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reads_nested_items_and_stops_at_padding() {
        let bytes = [0x30, 0x03, 0x02, 0x01, 0x10, 0x04, 0x01, 0xFF, 0x00, 0x00];

        let items = children(&bytes).unwrap();

        assert_eq!(
            items,
            [
                Tlv {
                    tag: 0x30,
                    value: &[0x02, 0x01, 0x10]
                },
                Tlv {
                    tag: 0x04,
                    value: &[0xFF]
                }
            ]
        );
        assert_eq!(
            unsigned(require(items[0].value, 0x02).unwrap()).unwrap(),
            0x10
        );
    }

    #[test]
    fn reads_long_form_length() {
        let mut bytes = vec![0x7A, 0x82, 0x01, 0x00];
        bytes.extend(std::iter::repeat_n(0xAB, 256));

        let items = children(&bytes).unwrap();

        assert_eq!(items[0].tag, 0x7A);
        assert_eq!(items[0].value.len(), 256);
    }

    #[test]
    fn reads_multi_byte_tag() {
        let items = children(&[0x9F, 0x65, 0x01, 0xFF]).unwrap();

        assert_eq!(items[0].tag, 0x9F65);
    }

    #[test]
    fn padded_length_needs_padding_to_be_sure() {
        let complete = [0x30, 0x01, 0xAA, 0x04, 0x00, 0x00, 0x00];

        assert_eq!(padded_length(&complete), Some(5));
        assert_eq!(padded_length(&complete[..5]), None);
        assert_eq!(padded_length(&[0x30, 0x05, 0xAA]), None);
    }

    #[test]
    fn single_item_length_waits_for_whole_value() {
        let header = [0x7A, 0x82, 0x00, 0x04, 0x01, 0x02];

        assert_eq!(single_item_length(&header), None);
        assert_eq!(single_item_length(&[0x7A, 0x02, 0x01, 0x02, 0x00]), Some(4));
    }

    #[test]
    fn rejects_truncated_value() {
        assert!(children(&[0x04, 0x05, 0x01]).is_err());
    }
}
