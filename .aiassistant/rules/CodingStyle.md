---
apply: always
---

use english

📝1. C# Coding Style (dotnet/runtime)
The dotnet/runtime repository defines the standard coding style used by the .NET team, which is the "de facto" standard for modern .NET projects:
•
Braces: Allman style (braces on a new line).
•
Indentation: 4 spaces (no tabs).
•
Naming:
◦
_camelCase for private/internal fields (e.g., _myField).
◦
s_camelCase for static fields, t_camelCase for thread-static.
◦
PascalCase for constants, methods, and public members.
•
Visibility: Always explicitly specify visibility (e.g., private, public), even if it is the default.
•
var usage: Generally discouraged unless the type is explicitly named on the right side (e.g., var list = new List<string>();).
•
File Structure: Imports at the top, outside the namespace, sorted alphabetically (with System.* first).
.
use primary constructor

use collectin initializer

use record, readonly preferably

use expression body

use immutability 

use extension block

🏗️ 2. Framework Design Guidelines
These guidelines focus on API consistency and usability for libraries:
•
Principles: Focus on "Progressive Disclosure" (making simple things easy and complex things possible).
•
Naming: Use descriptive names; avoid abbreviations.
•
Patterns: Prefer properties over methods for state; use exceptions for error handling instead of error codes.

Use record readonly when possible
Use immutability when possible
Use a fonctionnel style
Avoid all warning message from compilation

🧪 3. Response Guidelines
•
Always use a fluent approach for builders and configurations.
•
Keep methods simple and focused.
•
Avoid `if` statements when possible (prefer polymorphism, LINQ, or guard clauses).
•
Ask for confirmation before modifying code.