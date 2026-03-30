Guidelines and Rules for the Project
Based on the investigation of the repository, here are the guidelines and rules identified for this .NET 10 C# project (Carotte):
🛠️ Technical Stack
•
Framework: .NET 10.0 (net10.0)
•
C# Version: 14.0
•
Implicit Usings: Enabled
•
Nullable Reference Types: Enabled
🧪 Development Rules (TDD)
The project follows specific TDD (Test-Driven Development) rules defined in .aiassistant/rules/TDDRules.md:
•
Minimal Implementation: Implement only the code necessary to make tests pass and compile.
•
Class Design: Do not add properties to classes unless explicitly instructed.
•
Single Responsibility Tests: Each unit test should verify only a single aspect; avoid multiple assertions in one test.
•
Dependency Management: Use Dependency Injection (DI) for dependent classes.
•
Mocking: Use Moq for mocking interfaces.
•
Test Coverage: Always include a success test case.
🏗️ Architecture & Core Components
The project is a RabbitMQ client wrapper for microservices communication with the following features (from README.md):
•
Observability: Built-in support for OpenTelemetry.
•
Messaging Interfaces:
◦
IConsumer<TMessage> with Task HandleAsync(TMessage message, CancellationToken cancellationToken).
◦
IProducer<TMessage>.
•
Base Classes: Provides Consumer and Producer abstract classes.
•
Configuration: Supports broker connections, exchanges, and queues configuration.
•
Automation: Automatic registration of consumers and producers in the DI container.
📂 Project Structure
•
Solution File: Carotte.slnx (using the new XML-based solution format).
•
Core Library: Carotte/Carotte.csproj.
•
Rules/Guidelines: Located in .aiassistant/rules/.