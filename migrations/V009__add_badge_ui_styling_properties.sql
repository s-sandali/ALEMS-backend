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

-- Update existing badges with default styling
UPDATE badges
SET icon_type = 'star',
    icon_color = '#8f8f3e',
    unlock_hint = 'Locked'
WHERE icon_type IS NULL OR icon_type = '';
