-- =============================================================================
-- Migration: V002__create_quiz_tables.sql
-- Feature:   SA-100 – Quiz CRUD Database Schema
-- Author:    BigO Dev Team
-- Date:      2026-03-28
-- =============================================================================
-- Naming convention: snake_case (consistent with the `algorithms` table)
-- Soft delete:       is_active BOOLEAN DEFAULT TRUE  (FALSE = deleted)
-- Idempotent:        safe to re-run; uses IF NOT EXISTS / DROP IF EXISTS
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Table: quizzes
-- One quiz is tied to one algorithm. Created by an admin (Users.Id).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS quizzes (
    quiz_id         INT             NOT NULL AUTO_INCREMENT,
    algorithm_id    INT             NOT NULL,
    created_by      INT             NOT NULL,              -- FK → Users(Id)
    title           VARCHAR(255)    NOT NULL,
    description     TEXT            NULL,
    time_limit_mins INT UNSIGNED    NULL,                  -- NULL = no time limit
    pass_score      TINYINT UNSIGNED NOT NULL DEFAULT 70,  -- percentage (0–100)
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                             ON UPDATE CURRENT_TIMESTAMP,

    CONSTRAINT pk_quizzes           PRIMARY KEY (quiz_id),
    CONSTRAINT fk_quizzes_algorithm FOREIGN KEY (algorithm_id)
        REFERENCES algorithms (algorithm_id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,
    CONSTRAINT fk_quizzes_creator   FOREIGN KEY (created_by)
        REFERENCES Users (Id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,
    CONSTRAINT chk_pass_score       CHECK (pass_score BETWEEN 0 AND 100)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='One quiz per algorithm, authored by an admin user.';

-- Index for common query: fetch all active quizzes for a given algorithm
DROP INDEX IF EXISTS idx_quizzes_algorithm_active ON quizzes;
CREATE INDEX idx_quizzes_algorithm_active
    ON quizzes (algorithm_id, is_active);

-- -----------------------------------------------------------------------------
-- Table: quiz_questions
-- Each question belongs to one quiz (multiple-choice, single correct answer).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS quiz_questions (
    question_id     INT             NOT NULL AUTO_INCREMENT,
    quiz_id         INT             NOT NULL,
    question_text   TEXT            NOT NULL,
    option_a        VARCHAR(500)    NOT NULL,
    option_b        VARCHAR(500)    NOT NULL,
    option_c        VARCHAR(500)    NOT NULL,
    option_d        VARCHAR(500)    NOT NULL,
    correct_option  CHAR(1)         NOT NULL,              -- 'A' | 'B' | 'C' | 'D'
    explanation     TEXT            NULL,                  -- shown after submission
    order_index     SMALLINT        NOT NULL DEFAULT 0,    -- display order within quiz
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_quiz_questions        PRIMARY KEY (question_id),
    CONSTRAINT fk_quiz_questions_quiz   FOREIGN KEY (quiz_id)
        REFERENCES quizzes (quiz_id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT chk_correct_option       CHECK (correct_option IN ('A','B','C','D'))
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='Multiple-choice questions belonging to a quiz.';

-- Index for common query: fetch ordered active questions for a quiz
DROP INDEX IF EXISTS idx_quiz_questions_quiz_active ON quiz_questions;
CREATE INDEX idx_quiz_questions_quiz_active
    ON quiz_questions (quiz_id, is_active, order_index);
