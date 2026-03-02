

# BigO Backend

**Algorithm Learning & Evaluation Management System**
ASP.NET Web API (.NET 8) + MySQL + Clerk Authentication

---

## 📌 Overview

The BigOS Backend is the REST API layer of the Algorithm Learning & Evaluation Management System.
It handles:

* User authentication & synchronization (Clerk)
* JWT validation & role-based authorization
* Algorithm simulation engine
* Quiz management & auto-grading
* XP and badge system
* Analytics and report generation
* CI/CD-ready containerized deployment

This backend is developed as part of **SE3022 – Case Study Project (Semester 1, 2026)**.

As defined in the SRS, the backend is built using **ASP.NET Web API (C#)** and **MySQL** .

---

## 🏗 Architecture

The backend is designed as:

* ASP.NET Web API (C#)
* ADO.NET for database access
* MySQL 8.0 relational database
* JWT-based authentication (validated via Clerk)
* Swagger for API documentation
* Dockerized for Azure deployment
* Integrated CI/CD via GitHub Actions 

---

## 🔐 Authentication & Authorization

The system enforces:

* JWT-based authentication
* Role-based authorization (Student / Admin)
* Protected endpoints returning:

  * `401 Unauthorized` for invalid/missing tokens
  * `403 Forbidden` for invalid role access 

Clerk manages token issuance.
The backend validates tokens via configured authority.

---

## 🌐 Core API Endpoints

All endpoints are prefixed with `/api`.

| Method | Endpoint                     | Description               | Role          |
| ------ | ---------------------------- | ------------------------- | ------------- |
| POST   | `/api/auth/register`         | Register new user         | Public        |
| POST   | `/api/auth/login`            | Authenticate & return JWT | Public        |
| GET    | `/api/algorithms`            | List algorithms           | Student/Admin |
| GET    | `/api/algorithms/{id}/steps` | Retrieve simulation steps | Student/Admin |
| POST   | `/api/quizzes/{id}/attempts` | Submit quiz attempt       | Student       |
| GET    | `/api/admin/analytics`       | Aggregated analytics      | Admin         |
| GET    | `/health`                    | Health check endpoint     | Public        |
| GET    | `/swagger`                   | API documentation         | Public        |

Full endpoint specification is defined in the SRS .

---

## 🧪 Testing Strategy

The backend includes:

* Unit Tests (xUnit)
* Integration Tests
* Selenium E2E Tests (via CI)
* Code Coverage (Coverlet ≥ 70%)
* Health Endpoint Monitoring

CI pipeline blocks merges if:

* Tests fail
* Coverage < 70% 

---

## 🔧 Environment Configuration

All sensitive configuration values must be stored in **environment variables**, not hardcoded in source code .

Required environment variables:

* `ConnectionStrings__DefaultConnection`
* `Clerk__Authority`
* `Clerk__Audience` (optional)
* `Frontend__BaseUrl`

Example local development configuration:

```bash
export ConnectionStrings__DefaultConnection="your_connection_string"
export Clerk__Authority="https://your-clerk-domain"
```

---

## 🚀 Running Locally

### 1️⃣ Install Dependencies

* .NET 8 SDK
* MySQL 8

### 2️⃣ Restore & Run

```bash
dotnet restore
dotnet build
dotnet run
```

Swagger UI will be available at:

```
https://localhost:<port>/swagger
```


## ☁ Deployment

Deployment pipeline includes:

* Build & test (dotnet build + dotnet test)
* Coverage reporting
* Docker image build
* Azure deployment
* Post-deployment `/health` validation 

---

## 📊 Health Monitoring

`GET /health`

Returns:

```json
{
  "status": "Healthy",
  "database": "Connected"
}
```

Used for Azure monitoring & CI validation.

---

## 📁 Repository Structure

```
/Controllers
/Services
/Middleware
/Models
/DTOs
/Data
/Tests
Dockerfile
Program.cs
```



