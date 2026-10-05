pub const PORT: u16 = 17739;

pub const DISPLAY_NAME: &str = "Plugin ký số";

pub const VERSION: &str = env!("CARGO_PKG_VERSION");

pub const DEFAULT_ORIGINS: &[&str] = &[
    "https://ksts.yna.io.vn",
    "https://v2.nhaplieu.com",
    "http://localhost:4200",
    "https://localhost:4200",
    "http://localhost:3000",
    "https://localhost:3000",
];

pub const SESSION_IDLE_TIMEOUT_MINUTES: u64 = 15;

pub const PREFLIGHT_TEST_DATA_SIZE: usize = 32;
