# AI Agent Instructions – Claims Management System

## Project Context
We are building a Claims Management System for a graduate full-stack developer take-home assessment.

## Technology Stack
- Frontend: Angular
- Backend: C# / ASP.NET Core Web API
- Database: SQLite
- ORM: Entity Framework Core
- API communication: REST/HTTP
- Version control: Git

## Architecture
Use a modular monolith architecture.

Keep responsibilities separated:
- Controllers: HTTP request handling
- Services: Business logic
- Models: Domain/database entities
- DTOs: Request and response contracts
- Data: Entity Framework Core configuration
- Enums: Fixed domain values
- Interfaces: Service contracts

## Development Rules
1. Explain proposed changes before implementing them.
2. Work in small, incremental steps.
3. Do not generate the entire application at once.
4. Do not introduce unnecessary dependencies.
5. Follow C# naming conventions.
6. Use asynchronous methods for database operations.
7. Use dependency injection.
8. Keep business logic out of controllers.
9. Validate all incoming requests.
10. Use appropriate HTTP status codes.
11. Do not hardcode secrets or credentials.
12. Do not modify unrelated files.
13. Do not commit changes unless explicitly requested.
14. Run dotnet build after meaningful backend changes.
15. Explain errors and how to resolve them.

## Assessment Priorities
Prioritise:
- Correct domain modelling
- Claim lifecycle and business rules
- Data integrity
- API design
- Error handling
- Auditability
- Liability exposure calculations
- Working local application

## AI Collaboration
For each task:
1. Explain the objective.
2. Propose an implementation plan.
3. Identify assumptions.
4. Implement only the agreed scope.
5. Explain the generated code.
6. Run relevant tests/builds.
7. Summarise modified files.
8. Suggest a meaningful Git commit message.

The developer must understand and review all generated code before accepting it.