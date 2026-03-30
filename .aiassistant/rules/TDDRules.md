---
apply: always
---

When implementing code for a unit test to pass consider these rules:
- You may only implement the code needed to make the code compile and the unit test to pass.
- Do not add properties to classes unless instructed so.
- Make sure that you only verify a single aspect, so do not combine multiple asserts into one unit test.
- If you need to create dependent classes, use a DI approach.
- Use MOQ for mocking interfaces.
- Always generate a success test case as well.