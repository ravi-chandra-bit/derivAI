# Contributing to DerivAI

Thank you for your interest in contributing to DerivAI! This document provides guidelines for contributing to the project.

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow

## How to Contribute

### Reporting Bugs

1. Check if the bug has already been reported in [Issues](https://github.com/yourusername/DerivAI/issues)
2. If not, create a new issue with:
   - Clear title and description
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, Python version)
   - Screenshots if applicable

### Suggesting Enhancements

1. Open an issue with the "enhancement" label
2. Describe the feature and its benefits
3. Provide examples or mockups if possible

### Pull Requests

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes following the coding standards below
4. Write or update tests
5. Ensure all tests pass
6. Commit with clear messages: `git commit -m "Add feature: description"`
7. Push to your fork: `git push origin feature/your-feature-name`
8. Open a Pull Request with:
   - Description of changes
   - Related issue number
   - Screenshots/demos if UI changes

## Coding Standards

### Python

- Follow PEP 8 style guide
- Use type hints
- Document functions with docstrings
- Maximum line length: 100 characters
- Use meaningful variable names

```python
def detect_breaks(state: TradeReconciliationState) -> TradeReconciliationState:
    """
    Detect field-level mismatches between trade confirmations.
    
    Args:
        state: Current LangGraph state with trade data
        
    Returns:
        Updated state with detected breaks
    """
    # Implementation
```

### C#

- Follow Microsoft C# coding conventions
- Use meaningful names (PascalCase for classes, camelCase for variables)
- Add XML documentation for public methods
- Use async/await for I/O operations

```csharp
/// <summary>
/// Analyzes trade confirmation discrepancies using AI.
/// </summary>
/// <param name="tradeId">Unique trade identifier</param>
/// <returns>Analysis result with break explanations</returns>
public async Task<IActionResult> AnalyzeBreak(string tradeId)
{
    // Implementation
}
```

### Razor Views

- Use semantic HTML5
- Follow Bootstrap 5 conventions
- Keep views simple (logic in controllers/services)
- Use meaningful CSS class names

## Testing

### Python Tests

```bash
cd python_agents
pytest tests/ -v
```

### .NET Tests

```bash
cd aspnet_frontend
dotnet test
```

## Commit Message Guidelines

Format: `<type>: <description>`

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

Examples:
- `feat: Add support for CDS trade confirmations`
- `fix: Resolve null reference in GetConfirmationPair`
- `docs: Update deployment instructions for Azure`

## Development Workflow

1. **Setup**: Follow README.md quick start guide
2. **Branch**: Create feature branch from `main`
3. **Develop**: Make changes with frequent commits
4. **Test**: Run all tests locally
5. **Document**: Update README/docs if needed
6. **PR**: Submit pull request for review

## Review Process

1. Automated checks must pass (tests, linting)
2. At least one maintainer approval required
3. Address review comments
4. Squash commits before merge

## Questions?

- Open a [Discussion](https://github.com/yourusername/DerivAI/discussions)
- Tag maintainers in issues
- Check existing documentation

Thank you for contributing! 🎉
