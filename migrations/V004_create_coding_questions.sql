-- Migration: V004_create_coding_questions
-- Feature 2.1: Coding Question Schema
-- Run this script once against the alems-database MySQL instance.

CREATE TABLE IF NOT EXISTS coding_questions (
    id                INT          NOT NULL AUTO_INCREMENT,
    title             VARCHAR(255) NOT NULL,
    description       TEXT         NOT NULL,
    input_example     TEXT         NULL,
    expected_output   TEXT         NULL,
    difficulty        ENUM('easy', 'medium', 'hard') NOT NULL DEFAULT 'easy',
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
