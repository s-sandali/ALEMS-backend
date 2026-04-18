-- =============================================================================
-- Migration: V001__create_base_schema.sql
-- Feature:   Base schema – Users and algorithms tables
-- Author:    BigO Dev Team
-- =============================================================================
-- Must run first. All subsequent migrations reference these two tables.
-- Idempotent: uses IF NOT EXISTS / INSERT IGNORE.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Table: Users
-- PascalCase column names match the ADO.NET repository queries throughout the
-- codebase (SELECT Id, ClerkUserId, Email, Role, XpTotal, IsActive, CreatedAt).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Users (
    Id           INT          NOT NULL AUTO_INCREMENT,
    ClerkUserId  VARCHAR(255) NULL     UNIQUE,
    Email        VARCHAR(255) NOT NULL UNIQUE,
    Role         VARCHAR(50)  NOT NULL DEFAULT 'User',
    XpTotal      INT          NOT NULL DEFAULT 0,
    IsActive     TINYINT(1)   NOT NULL DEFAULT 1,
    CreatedAt    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
                                       ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (Id),
    INDEX idx_users_clerk_id  (ClerkUserId),
    INDEX idx_users_email     (Email),
    INDEX idx_users_role      (Role)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- Table: algorithms
-- snake_case column names used throughout the back-end repositories.
-- PK is algorithm_id – referenced by quizzes, quiz_attempts, badges FKs.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS algorithms (
    algorithm_id             INT          NOT NULL AUTO_INCREMENT,
    name                     VARCHAR(100) NOT NULL UNIQUE,
    category                 VARCHAR(100) NOT NULL,
    description              TEXT         NOT NULL,
    time_complexity_best     VARCHAR(50)  NOT NULL,
    time_complexity_average  VARCHAR(50)  NOT NULL,
    time_complexity_worst    VARCHAR(50)  NOT NULL,
    created_at               DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (algorithm_id),
    INDEX idx_algorithms_category (category)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

-- -----------------------------------------------------------------------------
-- Seed: core algorithms (V004 adds more; use INSERT IGNORE to stay idempotent)
-- -----------------------------------------------------------------------------
INSERT IGNORE INTO algorithms (name, category, description, time_complexity_best, time_complexity_average, time_complexity_worst, created_at)
VALUES
    ('Bubble Sort',
     'Sorting',
     'Repeatedly steps through the list, compares adjacent elements and swaps them if they are in the wrong order.',
     'O(n)', 'O(n^2)', 'O(n^2)',
     CURRENT_TIMESTAMP),

    ('Binary Search',
     'Searching',
     'Searches a sorted array by repeatedly dividing the search interval in half.',
     'O(1)', 'O(log n)', 'O(log n)',
     CURRENT_TIMESTAMP),

    ('Linear Search',
     'Searching',
     'Sequentially checks each element of the list until a match is found or the list is exhausted.',
     'O(1)', 'O(n)', 'O(n)',
     CURRENT_TIMESTAMP);
