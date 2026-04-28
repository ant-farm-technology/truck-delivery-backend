use redis::{aio::ConnectionManager, AsyncCommands};
use serde::{de::DeserializeOwned, Serialize};

pub struct Cache {
    conn: ConnectionManager,
}

impl Cache {
    pub fn new(conn: ConnectionManager) -> Self {
        Self { conn }
    }

    pub async fn get<T: DeserializeOwned>(&mut self, key: &str) -> Option<T> {
        let raw: Option<String> = self.conn.get(key).await.ok()?;
        raw.and_then(|s| serde_json::from_str(&s).ok())
    }

    pub async fn set<T: Serialize>(&mut self, key: &str, value: &T, ttl_secs: u64) {
        if let Ok(json) = serde_json::to_string(value) {
            let _: Result<(), _> = self.conn.set_ex(key, json, ttl_secs).await;
        }
    }
}
