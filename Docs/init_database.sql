-- =============================================================
-- RogueGame 云存档数据库 初始化脚本
-- 适用：MySQL 8.0+（已在 8.0.41 语法下编写）
-- 用法：mysql -u root -p  →  source 本文件路径
--
-- 【本脚本分两部分】
--   A. 建表段：全部 IF NOT EXISTS，可重复执行。
--   B. 升级段：只给"已经建过库"的老库用的一次性语句，新库跳过。
--      （如 ALTER TABLE ADD COLUMN 重复执行会报 Duplicate column）
--      某个升级段落执行过一次后，请把它整段注释掉，避免误执行。
-- =============================================================

CREATE DATABASE IF NOT EXISTS rogue_game
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_unicode_ci;

USE rogue_game;

-- =============================================================
-- A. 建表（幂等）
-- =============================================================

-- 1. 账号表（含金币余额）
CREATE TABLE IF NOT EXISTS accounts (
    id            BIGINT        NOT NULL AUTO_INCREMENT,
    username      VARCHAR(32)   NOT NULL,
    password_hash VARBINARY(64) NOT NULL COMMENT 'PBKDF2(password, salt, 10000)',
    salt          VARBINARY(16) NOT NULL COMMENT '每账号独立随机盐',
    coins         INT           NOT NULL DEFAULT 0,
    status        TINYINT       NOT NULL DEFAULT 1 COMMENT '1正常 0封禁',
    created_at    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at DATETIME      NULL,
    pending_session TEXT       NULL COMMENT '未结算本局进度快照(JSON)，局末清空',
    PRIMARY KEY (id),
    UNIQUE KEY uk_accounts_username (username)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='账号表';

-- 2. 角色拥有关系（替代 PlayerPrefs: CharOwned_{uid}）
CREATE TABLE IF NOT EXISTS character_ownership (
    account_id    BIGINT      NOT NULL,
    character_uid VARCHAR(64) NOT NULL COMMENT '对应 CharacterBlueprint.uid',
    owned         TINYINT     NOT NULL DEFAULT 0,
    acquired_at   DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (account_id, character_uid),
    CONSTRAINT fk_char_own_account FOREIGN KEY (account_id)
        REFERENCES accounts (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='角色拥有关系';

-- 3. 铭文解锁（替代 PlayerPrefs: Engravings_Unlocked）
CREATE TABLE IF NOT EXISTS engraving_unlocked (
    account_id   BIGINT      NOT NULL,
    engraving_id VARCHAR(64) NOT NULL COMMENT '对应 EngravingBlueprint.engravingId',
    unlocked_at  DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (account_id, engraving_id),
    CONSTRAINT fk_eng_unlock_account FOREIGN KEY (account_id)
        REFERENCES accounts (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='已解锁铭文';

-- 4. 铭文装备槽（替代 PlayerPrefs: Engravings_Selected，最多 3 个）
CREATE TABLE IF NOT EXISTS engraving_selected (
    account_id   BIGINT      NOT NULL,
    engraving_id VARCHAR(64) NOT NULL,
    slot_index   TINYINT     NOT NULL COMMENT '0..2',
    PRIMARY KEY (account_id, engraving_id),
    UNIQUE KEY uk_eng_slot (account_id, slot_index),
    CONSTRAINT fk_eng_sel_account FOREIGN KEY (account_id)
        REFERENCES accounts (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='已装备铭文（上限3）';

-- =============================================================
-- B. 老库升级段（一次性，新库不要执行；执行过请整段注释）
-- =============================================================

-- -------------------------------------------------------------
-- B1. 老库升级：accounts 增加"本局进度快照"列
--     新库上面的 CREATE TABLE 已经带了这一列，只有旧库需要执行
--     （少了这列，服务端所有查 accounts 的接口都会报 Unknown column）
-- -------------------------------------------------------------
ALTER TABLE accounts
  ADD COLUMN pending_session TEXT NULL COMMENT '未结算本局进度快照(JSON)，局末清空'
  AFTER last_login_at;

-- -------------------------------------------------------------
-- B2. 老库升级：删除 shop_purchases 表
--     该表从未被写入过有效数据（商店在售商品均为单局生效，走 accounts.pending_session），
--     永久购买标记机制已整体移除
-- -------------------------------------------------------------
DROP TABLE IF EXISTS shop_purchases;

-- -------------------------------------------------------------
-- B3. 老库升级：删除 run_records 表
--     该表从未被读写过（/run/settle 只加金币，不落战绩），
--     战绩/排行榜功能已放弃，属死表
-- -------------------------------------------------------------
DROP TABLE IF EXISTS run_records;

-- =============================================================
-- 后端专用账号（不要用 root 连接生产库）
-- 如需局域网内的其他设备访问，把 'localhost' 改成 '%'
-- =============================================================
-- CREATE USER IF NOT EXISTS 'rogue'@'localhost' IDENTIFIED BY '改成你的密码';
-- GRANT SELECT, INSERT, UPDATE, DELETE ON rogue_game.* TO 'rogue'@'localhost';
-- FLUSH PRIVILEGES;

-- 验证
SHOW TABLES;
