# Tech Stack Overview
This document outlines the technologies chosen for the RSSVibe project and explains which technology is used in each component of the system.

## 1. Runtime and Language

- **.NET 10 & C# 14**: 
  - The project is built using the latest .NET runtime and C# language features. This provides a high-performance, modern, and secure runtime environment for both the backend API and any heavy computational tasks.

## 2. Frontend

- **Blazor**: 
  - The frontend is implemented using Blazor, which allows for interactive web applications using C#. This provides a seamless development experience and enables code sharing between the frontend and backend where appropriate.
  - For UI components and styling, we utilize **Fluent UI**, a popular Blazor component library that offers a wide range of pre-built, customizable UI elements. This accelerates development and ensures a consistent look and feel across the application.
  - For render modes - for the most part we will try to use `Interactive auto` mode, which allows Blazor to decide the best rendering strategy based on the user's device and network conditions. This mode provides a balance between performance and interactivity, ensuring a smooth user experience across various scenarios. For specific pages that require high interactivity, we may opt for `Server` mode to leverage server-side processing and reduce client-side load.

## 3. Backend API

- **ASP.NET Core**: 
  - The API layer is developed using ASP.NET Core. This framework delivers high performance, scalability, and security. It also integrates well with other parts of the Microsoft ecosystem.
  - For endpoints implementation we will use **Minimal APIs**, which provide a lightweight and efficient way to create HTTP APIs with minimal boilerplate code. This approach is ideal for building simple and fast endpoints, making it easier to maintain and scale the API as needed.
  - For request validation we use **FluentValidation** with automatic wiring provided by **SharpGrip.FluentValidation.AutoValidation.Endpoints**, ensuring Minimal APIs run validators before handlers execute without repetitive plumbing.
  - For endpoint security and user management, we use **ASP.NET Identity**, which provides a robust and flexible authentication and authorization system.
  - For unit and integration tests we will use **TUnit**, a testing framework designed for .NET applications. It offers a simple and effective way to write and run integration tests, ensuring the reliability and correctness of the API.
  - For integration tests we will use WebApplicationFactory, which is part of the ASP.NET Core testing framework. It allows for in-memory hosting of the web application during tests, enabling comprehensive integration testing without the need for a full deployment.
  - For running the database in tests we will use **Testcontainers**, a .NET library that provides lightweight, disposable instances of common databases and services using Docker containers. This allows for consistent and isolated testing environments, ensuring that tests are reliable and reproducible.

## 4. Database and Persistence

- **PostgreSQL 18**: 
  - PostgreSQL serves as the primary data storage solution. It is robust, scalable, and well-suited for handling complex queries and transactions related to RSS feed management and user data.

## 5. Object-Relational Mapping (ORM)

- **Entity Framework Core 10**: 
  - Entity Framework Core provides a convenient and efficient way to interact with the PostgreSQL database, allowing for code-first development and LINQ-based queries.

## 6. Caching

- **Redis 8**: 
  - Redis is used for caching frequently accessed data. This helps reduce database load and improve the performance of the application, especially under high concurrency.
  - Over the redis we will use **FusionCache**, a distributed caching library for .NET applications. It simplifies the process of caching data in Redis, providing an easy-to-use API for storing and retrieving cached items. FusionCache acts as an implementation of **HybridCache**, which combines in-memory caching with distributed caching to optimize performance and scalability.

## 7. Task Scheduling

- **TickerQ**: 
  - TickerQ is utilized for scheduling background tasks such as periodic content parsing. It offers a flexible and reliable mechanism to trigger scheduled jobs in a scalable manner.

## 8. AI-Powered Operations

- **SemanticKernel (or Microsoft Agentic Framework)**: 
  - These technologies are leveraged for AI-driven analysis and processing, such as detecting article containers on websites. They provide the logic and algorithms needed for the AI-powered feed generation.

## 9. LLM Model Access

- **OpenRouter**: 
  - OpenRouter provides access to LLM models, enabling the system to perform advanced natural language processing tasks. It is used to interface with large language models required for content analysis and automated configuration.

## 10. Local Development Environment

- **.NET Aspire**: 
  - For local development, we use .NET Aspire, a lightweight and efficient environment that simplifies the setup and management of .NET applications. It provides tools and features that enhance the developer experience, making it easier to build, test, and deploy the application locally.
  - For remote deployments we will generate docker compose yaml files directly from the .NET Aspire project configuration using Aspire Docker Compose Publisher.

## Summary

The updated tech stack is designed to meet both immediate MVP demands and long-term goals of performance, scalability, and innovation:

- **Rapid Development and Robustness**: Leveraging .NET 10 and C# 14, along with ASP.NET Core and Blazor, the stack accelerates development while ensuring a robust, secure foundation.
- **Efficient Data Management & Scalability**: PostgreSQL 18 combined with Entity Framework Core 10 guarantees efficient data handling, while an advanced caching strategy using Redis 8 paired with FusionCache (implementing HybridCache) optimizes performance under high concurrency.
- **Advanced AI & Automation**: Integration of SemanticKernel (or Microsoft Agentic Framework) and OpenRouter enables cutting-edge AI for content analysis and automated configuration, enhancing the system's intelligence.
- **Reliable Task Scheduling**: TickerQ manages periodic background tasks seamlessly, ensuring operational efficiency and timely content parsing.
- **Optimized Development Experience**: .NET Aspire delivers a consistent and streamlined local development environment, supporting rapid iterations and comprehensive testing.

This holistic approach ensures that every component—from AI-driven operations to advanced caching and minimal API design—works in concert to deliver a secure, scalable, and high-performing application.
