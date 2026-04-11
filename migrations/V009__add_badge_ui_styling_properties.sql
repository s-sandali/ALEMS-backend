-- =============================================================================
-- Migration: V009__add_badge_ui_styling_properties.sql
-- Feature:   Add UI styling properties to badges
-- Author:    BigO Dev Team
-- Date:      2026-04-11
-- =============================================================================
-- Adds icon_type, icon_color, and unlock_hint columns to badges table.
-- These columns support rich UI rendering of badges in the frontend.
-- Idempotent: safe to re-run; uses IF NOT EXISTS check.
-- =============================================================================

-- Add UI styling columns to badges table
ALTER TABLE badges ADD COLUMN IF NOT EXISTS icon_type VARCHAR(50) DEFAULT 'star' COMMENT 'Icon type for lucide-react icons: star, bolt, shield, etc.';
ALTER TABLE badges ADD COLUMN IF NOT EXISTS icon_color VARCHAR(20) DEFAULT '#8f8f3e' COMMENT 'Icon color in hex format';
ALTER TABLE badges ADD COLUMN IF NOT EXISTS unlock_hint VARCHAR(100) DEFAULT 'Locked' COMMENT 'Hint text for locked badges';

-- Backfill known badge styles so the frontend can render distinct icons immediately.
UPDATE badges
SET icon_type = CASE badge_name
        WHEN 'First Steps' THEN 'star'
        WHEN 'Quick Learner' THEN 'bolt'
        WHEN 'Problem Solver' THEN 'shield'
        WHEN 'Algorithm Ace' THEN 'trophy'
        WHEN 'Big O Master' THEN 'gauge'
        ELSE COALESCE(NULLIF(icon_type, ''), 'star')
    END,
    icon_color = CASE badge_name
        WHEN 'First Steps' THEN '#f6c945'
        WHEN 'Quick Learner' THEN '#7df9ff'
        WHEN 'Problem Solver' THEN '#7fe7a2'
        WHEN 'Algorithm Ace' THEN '#c8ff3e'
        WHEN 'Big O Master' THEN '#ff9f5a'
        ELSE COALESCE(NULLIF(icon_color, ''), '#8f8f3e')
    END,
    unlock_hint = CASE badge_name
        WHEN 'First Steps' THEN 'Reach 50 XP'
        WHEN 'Quick Learner' THEN 'Reach 150 XP'
        WHEN 'Problem Solver' THEN 'Reach 300 XP'
        WHEN 'Algorithm Ace' THEN 'Reach 600 XP'
        WHEN 'Big O Master' THEN 'Reach 1000 XP'
        ELSE COALESCE(NULLIF(unlock_hint, ''), 'Locked')
    END;
