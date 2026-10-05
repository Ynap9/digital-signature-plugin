use std::time::SystemTime;

use chrono::{DateTime, TimeZone};
use ks_plugin_shared::dto::{CertSource, SignCertDto};

use super::CertificateDetails;

const DATE_FORMAT: &str = "%d/%m/%Y %H:%M:%S";
const KEY_USAGE_REASON: &str =
    "Chứng thư số không được cấp quyền ký (KeyUsage thiếu digitalSignature/nonRepudiation).";

pub fn card_certificate_dto<Tz>(
    details: &CertificateDetails,
    key_provider: &str,
    now: SystemTime,
    zone: &Tz,
) -> SignCertDto
where
    Tz: TimeZone,
    Tz::Offset: std::fmt::Display,
{
    let format = |time: SystemTime| {
        DateTime::<chrono::Utc>::from(time)
            .with_timezone(zone)
            .format(DATE_FORMAT)
            .to_string()
    };
    let valid_from = format(details.not_before);
    let valid_to = format(details.not_after);
    let is_expired = !details.is_valid_at(now);
    let reason = if is_expired {
        Some(format!(
            "Chứng thư số ngoài thời hạn hiệu lực ({valid_from} - {valid_to})."
        ))
    } else if !details.allows_signing {
        Some(KEY_USAGE_REASON.to_owned())
    } else {
        None
    };

    SignCertDto {
        subject: details.subject.clone(),
        common_name: details.common_name.clone(),
        issuer: details.issuer.clone(),
        issuer_common_name: details.issuer_common_name.clone(),
        serial_number: details.serial_number.clone(),
        thumbprint: details.thumbprint.clone(),
        source: CertSource::UsbToken,
        key_provider: Some(key_provider.to_owned()),
        valid_from,
        valid_to,
        has_private_key: true,
        is_expired,
        allows_signing: details.allows_signing,
        reason,
    }
}
