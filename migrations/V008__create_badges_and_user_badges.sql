-- =============================================================================
-- Migration: V008__create_badges_and_user_badges.sql
-- Feature:   Badges & User Badge Awards
-- Author:    BigO Dev Team
-- Date:      2026-04-10
-- =============================================================================
-- Adds badge definitions with XP thresholds and a user_badges join table.
-- Idempotent: safe to re-run; uses IF NOT EXISTS and ON DUPLICATE KEY UPDATE.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Table: badges
-- Stores the badge catalog and required XP threshold for each badge.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS badges (
    badge_id           INT NOT NULL AUTO_INCREMENT,
    badge_name         VARCHAR(100) NOT NULL,
    badge_description  VARCHAR(255) NOT NULL,
    xp_threshold       INT UNSIGNED NOT NULL,
    created_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (badge_id),
    UNIQUE KEY uq_badges_name (badge_name)
) ENGINE=InnoDB;

-- -----------------------------------------------------------------------------
-- Table: user_badges
-- Tracks badge awards for users.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS user_badges (
    user_badge_id      INT NOT NULL AUTO_INCREMENT,
    user_id            INT NOT NULL,
    badge_id           INT NOT NULL,
    awarded_at         DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (user_badge_id),
    UNIQUE KEY uq_user_badges_user_badge (user_id, badge_id),

    FOREIGN KEY (user_id)
        REFERENCES Users (Id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    FOREIGN KEY (badge_id)
        REFERENCES badges (badge_id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE INDEX idx_user_badges_user
    ON user_badges (user_id, awarded_at);

CREATE INDEX idx_user_badges_badge
    ON user_badges (badge_id);

-- -----------------------------------------------------------------------------
-- Seed: badge catalog
-- At least 5 default badges with XP thresholds.
-- -----------------------------------------------------------------------------
INSERT INTO badges (badge_name, badge_description, xp_threshold)
VALUES
    ('First Steps',     'Earned after reaching 50 XP.',     50),
    ('Quick Learner',   'Earned after reaching 150 XP.',   150),
    ('Problem Solver',  'Earned after reaching 300 XP.',   300),
    ('Algorithm Ace',   'Earned after reaching 600 XP.',   600),
    ('Big O Master',    'Earned after reaching 1000 XP.', 1000)
ON DUPLICATE KEY UPDATE
    badge_description = VALUES(badge_description),
    xp_threshold = VALUES(xp_threshold);
