# AcxiomCRM Enterprise — Production Web Application

An enterprise-grade, role-based Customer Relationship Management (CRM) platform engineered with a strict layered architecture in **ASP.NET Core 8**, **Entity Framework Core**, **ASP.NET Core Identity**, and **Bootstrap 5**.

---

## 🌟 Executive Overview & Key Features

- **Multi-Tenant / Role-Based Access Control (RBAC):**
  - **Admin:** Full organization management, user provisioning, security audit log access, data management, and executive analytics.
  - **Manager:** Team pipeline visibility, lead conversion analytics, sales performance tracking, and management reports.
  - **Sales Executive:** Scoped strictly to assigned customer accounts, leads, opportunities, and personal touchpoint schedules.
- **Customer 360 & Relationship Management:**
  - Complete master data tracking with customer code generator (`CUST-xxxx`), client & server-side duplicate prevention (email & phone uniqueness), and relational 360-degree timeline.
- **Lead Lifecycle & 1-Click Conversion Workflow:**
  - Prospect stages: `New` &rarr; `Contacted` &rarr; `Qualified` &rarr; `Converted` / `Lost`.
  - Automated 1-click conversion turning qualified prospects into active Customer profiles and connected sales Opportunities with audit trail.
- **Opportunity & Pipeline Management:**
  - Full stage tracking: `Qualification`, `Proposal`, `Negotiation`, `Won`, `Lost`.
  - Derived probability weighting (`Amount × Probability / 100`).
  - Strict business rules: `Amount > 0`, `Probability` between 0–100%, and `ExpectedCloseDate` strictly forward-dated for active deals.
- **Follow-Up & Omnichannel Activity Tracking:**
  - Touchpoints: `Call`, `Meeting`, `Email`, `Demo`, `Task`.
  - Date rule enforcement: Planned touchpoints cannot be scheduled earlier than today.
  - Overdue touchpoints alert ribbon, one-click completion, and reschedule workflows.
- **Tamper-Evident Security Audit Trail:**
  - Append-only logging tracking user credentials, action types (`Login`, `Failed Login`, `Create`, `Update`, `Delete`, `Role Change`, `Lockout`), old vs. new JSON snapshots, and IP addresses.
- **Analytics Dashboard & Visualizations:**
  - Role-scoped KPI cards: Total Customers, Open Leads, Won/Lost Deals, Pipeline & Weighted Values.
  - Interactive **Chart.js** visualizations: Opportunity Pipeline Stage Breakdown, Lead Status Distribution, and Monthly Sales outcome trends.
- **Production REST API & Swagger:**
  - Clean separation using DTOs at `api/customers`, `api/leads`, `api/opportunities`, `api/followups`, `api/auth`, and `api/reports/pipeline` with Swagger UI at `/api/docs`.
- **Exportable Business Intelligence:**
  - Filterable, searchable reporting suite with real-time CSV spreadsheet downloads.

---

## 🏛️ Project Architecture: Frontend vs. Backend Separation

AcxiomCRM implements a decoupled, multi-tiered enterprise architecture. Following the exact **Section 17.17** evaluation standards of the Acxiom specification, the **Frontend (Presentation Tier)** and **Backend (Business & Data Tier)** maintain strict separation of concerns:

### Architecture Tier Mapping

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                        FRONTEND (Presentation Tier)                          │
│                                                                              │
│   📁 Views/ (Razor Server-Side Rendered UI Templates)                        │
│       ├── Account/        -> Login, Register, Google OAuth Connect Screen    │
│       ├── Dashboard/      -> Executive KPI Cards & Visual Analytics View     │
│       ├── Customer/       -> Customer 360 Portal, Master Profiles, History   │
│       ├── Lead/           -> Lead Pipeline & 1-Click Conversion UI           │
│       ├── Opportunity/    -> Sales Deals & Weighted Revenue Forecasts        │
│       ├── FollowUp/       -> Touchpoint Schedules & Overdue Warning Ribbons  │
│       ├── Activity/       -> Omnichannel Call, Email, Meeting History Feed   │
│       ├── Reports/        -> Funnel Analytics & CSV Export Buttons           │
│       ├── UserManagement/ -> Admin User & Role Governance Screens            │
│       └── Shared/         -> Master Navigation Layout, Menus, Modals         │
│                                                                              │
│   📁 wwwroot/ (Static Client-Side Assets)                                    │
│       ├── css/  -> Custom styles, dark-accent headers & CRM theming          │
│       ├── js/   -> Interactive dynamic DOM, Chart.js graphs, form validators │
│       └── lib/  -> Bootstrap 5, jQuery, Unobtrusive Validation library       │
└──────────────────────────────────────────────────────────────────────────────┘
                                    ▲
                                    │ HTTP Requests / ViewModels / REST APIs
                                    ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                         BACKEND (Server & Logic Tier)                        │
│                                                                              │
│   📁 Controllers/ (Application & Routing Orchestration)                     │
│       ├── Web Controllers -> Account, Customer, Lead, Opportunity, FollowUp │
│       └── Api/            -> Secure Headless REST Controllers (/api/*)       │
│                                                                              │
│   📁 Services/ (Business Logic, Workflows & Calculations)                    │
│       ├── ICustomerService    -> Customer validations & de-duplication       │
│       ├── ILeadService        -> Lead lifecycle & atomic 1-click conversion  │
│       ├── IOpportunityService -> Pipeline stages & probability weighting     │
│       ├── IFollowUpService    -> Overdue detection & future-date enforcement │
│       ├── IDashboardService   -> Role-scoped KPI aggregation                 │
│       ├── IReportService      -> Funnel analytics & RFC-4180 CSV streaming   │
│       └── IAuditService       -> Append-only logging with JSON diff snapshots│
│                                                                              │
│   📁 Models/ (Domain Entities, ViewModels & DTOs)                            │
│       ├── Core Entities -> Customer, Lead, Opportunity, FollowUp, Activity   │
│       ├── Identity      -> ApplicationUser (PBKDF2), ApplicationRole         │
│       ├── ViewModels/   -> Strongly-typed models for Razor views             │
│       └── DTOs/         -> Clean contract models for REST APIs               │
│                                                                              │
│   📁 Data/ (Data Access & Persistence Tier)                                  │
│       ├── ApplicationDbContext -> EF Core 8 SQLite relational configuration │
│       └── DbInitializer        -> Automated schema creation & seed engine    │
└──────────────────────────────────────────────────────────────────────────────┘
```

| Component | Physical Path | Primary Responsibility | Technology |
| :--- | :--- | :--- | :--- |
| **Frontend UI** | [Views/](file:///c:/CRM_ACXIOM/Views/) | Razor templates, layout partials, and modals | Razor (.cshtml), HTML5 |
| **Frontend Assets** | [wwwroot/](file:///c:/CRM_ACXIOM/wwwroot/) | Styling, animations, client-side validation, graphs | Bootstrap 5, Chart.js, jQuery |
| **Backend API** | [Controllers/Api/](file:///c:/CRM_ACXIOM/Controllers/Api/) | Headless RESTful JSON endpoints | ASP.NET Core Web API, Swagger |
| **Backend Controllers** | [Controllers/](file:///c:/CRM_ACXIOM/Controllers/) | Request routing, anti-forgery, model binding | ASP.NET Core MVC |
| **Backend Services** | [Services/](file:///c:/CRM_ACXIOM/Services/) | Business logic, state machines, math formulas | C# Business Logic Layer |
| **Backend Data Access** | [Data/](file:///c:/CRM_ACXIOM/Data/) | EF Core DbContext, relationships, migration | EF Core 8, SQLite |
| **Domain Models** | [Models/](file:///c:/CRM_ACXIOM/Models/) | Entities, Identity classes, ViewModels, DTOs | C# POCO Classes |

---

## 🔑 Default Seed Credentials

| Role | Corporate Email | Password | Primary Scope |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiom.com` | `Admin@12345` | Full Organization & Security Administration |
| **Manager** | `manager@acxiom.com` | `Manager@12345` | Team Pipeline & Reporting Oversight |
| **Sales Executive** | `sales@acxiom.com` | `Sales@12345` | Assigned Customers, Leads & Activities |

*(All login pages feature 1-click credential auto-fill buttons for instant reviewer switching!)*

---

## 🚀 Running the Application Locally

1. **Verify .NET 8 SDK:**
   ```powershell
   dotnet --version
   ```

2. **Restore & Build Solution:**
   ```powershell
   dotnet build
   ```

3. **Launch Web Server:**
   ```powershell
   dotnet run --launch-profile http
   ```
   Navigate to: **`http://localhost:5164`**

4. **Access REST API Documentation (Swagger):**
   Navigate to: **`http://localhost:5164/api/docs`**

5. **Run Automated Acceptance Verification Suite:**
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\verify_acceptance.ps1
   ```

---

## 📋 Acceptance Verification Results

All 14 Acceptance Criteria specified in Section 17.19 of the project documentation have been executed and passed:

1. `[PASS]` Unauthenticated request to `/Customer` redirects to `/Account/Login`
2. `[PASS]` Admin credentials authenticate successfully and redirect to `/Dashboard`
3. `[PASS]` Customer Create view renders client-side validation rules and data-val attributes
4. `[PASS]` Server-side validation rejects invalid email and phone when browser rules bypassed
5. `[PASS]` Opportunity with Amount &le; 0 is rejected with exact requirement message
6. `[PASS]` Opportunity with Probability > 100 is rejected with exact requirement message
7. `[PASS]` Opportunity with past Expected Close Date is rejected for active deal
8. `[PASS]` Follow-Up scheduled prior to today is rejected with exact requirement message
9. `[PASS]` SalesExecutive login scopes view to assigned representative data
10. `[PASS]` Manager login provides access to team pipeline analytics and reports
11. `[PASS]` Admin login provides full access to User Administration and Security Audit Logs
12. `[PASS]` Creating a CRM customer generates an automated entry in the Security Audit Trail
13. `[PASS]` REST endpoint `/api/customers` returns valid JSON payload with status 200 OK
14. `[PASS]` Dashboard renders all mandatory KPI cards and Chart.js visualization canvases
