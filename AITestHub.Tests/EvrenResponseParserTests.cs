using AITestHub.DTOs;
using AITestHub.Entities.Enums;
using AITestHub.Services;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Unit tests for the LLM response parsing logic of EvrenTestCaseGeneratorService.
/// </summary>
public class EvrenResponseParserTests
{
    private const string CleanJson =
        """
        [
          {
            "title": "Valid Login",
            "description": "Enter valid credentials.",
            "preconditions": "A registered user exists.",
            "steps": "1. Open the login page.\n2. Enter credentials.\n3. Submit.",
            "expectedResult": "The user is authenticated.",
            "priority": "High",
            "testType": "Positive"
          },
          {
            "title": "Invalid Login",
            "description": "Enter invalid credentials.",
            "preconditions": null,
            "steps": "1. Open the login page.\n2. Enter invalid credentials.\n3. Submit.",
            "expectedResult": "An error is shown.",
            "priority": "Low",
            "testType": "Negative"
          }
        ]
        """;

    [Fact]
    public void ParseTestCases_WithCleanJsonArray_ShouldReturnAllTestCases()
    {
        var result = EvrenTestCaseGeneratorService.ParseTestCases(CleanJson);

        Assert.Equal(2, result.Count);
        Assert.Equal("Valid Login", result[0].Title);
        Assert.Equal(Priority.High, result[0].Priority);
        Assert.Equal(TestType.Positive, result[0].TestType);
        Assert.Equal(Priority.Low, result[1].Priority);
        Assert.Equal(TestType.Negative, result[1].TestType);
        Assert.Null(result[1].Preconditions);
    }

    [Fact]
    public void ParseTestCases_WithMarkdownCodeFence_ShouldStripFenceAndParse()
    {
        var fenced = $"```json\n{CleanJson}\n```";

        var result = EvrenTestCaseGeneratorService.ParseTestCases(fenced);

        Assert.Equal(2, result.Count);
        Assert.Equal("Valid Login", result[0].Title);
    }

    [Fact]
    public void ParseTestCases_WithSurroundingExplanationText_ShouldExtractJsonArray()
    {
        var withText = $"Here are the test cases you requested:\n{CleanJson}\nLet me know if you need more.";

        var result = EvrenTestCaseGeneratorService.ParseTestCases(withText);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseTestCases_WithCaseInsensitiveFields_ShouldBindCorrectly()
    {
        var json = """
        [
          {
            "Title": "T",
            "DESCRIPTION": "D",
            "steps": "S",
            "expectedresult": "E",
            "priority": "Critical",
            "testtype": "EdgeCase"
          }
        ]
        """;

        var result = EvrenTestCaseGeneratorService.ParseTestCases(json);

        Assert.Single(result);
        Assert.Equal("T", result[0].Title);
        Assert.Equal("D", result[0].Description);
        Assert.Equal("S", result[0].Steps);
        Assert.Equal("E", result[0].ExpectedResult);
        Assert.Equal(Priority.Critical, result[0].Priority);
        Assert.Equal(TestType.EdgeCase, result[0].TestType);
    }

    [Fact]
    public void ParseTestCases_WithEmptyJsonArray_ShouldThrowConfigurationException()
    {
        Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases("[]"));
    }

    [Fact]
    public void ParseTestCases_WhenContentIsNotJson_ShouldThrowConfigurationException()
    {
        Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases("I could not generate any test cases, sorry."));
    }

    [Fact]
    public void ParseTestCases_WithInvalidJsonInsideArray_ShouldThrowConfigurationException()
    {
        Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases("[ { broken json } ]"));
    }

    [Fact]
    public void ParseTestCases_WithInvalidPriorityValue_ShouldThrowConfigurationException()
    {
        var json = CleanJson.Replace("\"High\"", "\"Urgent\"");

        var ex = Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases(json));
    }

    [Fact]
    public void ParseTestCases_WithMissingPriority_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "T", "description": "D", "steps": "S", "expectedResult": "E", "testType": "Positive" }
        ]
        """;

        var ex = Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases(json));
        Assert.Contains("priority", ex.Message);
    }

    [Fact]
    public void ParseTestCases_WithMissingTestType_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "T", "description": "D", "steps": "S", "expectedResult": "E", "priority": "High" }
        ]
        """;

        var ex = Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases(json));
        Assert.Contains("testType", ex.Message);
    }

    [Fact]
    public void ParseTestCases_WithMissingSteps_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "T", "description": "D", "expectedResult": "E", "priority": "High", "testType": "Positive" }
        ]
        """;

        var ex = Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases(json));
        Assert.Contains("steps", ex.Message);
    }

    [Fact]
    public void ParseTestCases_WithIncompleteTestCases_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "Valid", "description": "D", "steps": "S", "expectedResult": "E", "priority": "High", "testType": "Positive" },
          { "title": "", "description": "D", "steps": "S", "expectedResult": "E", "priority": "High", "testType": "Positive" }
        ]
        """;

        Assert.Throws<EvrenConfigurationException>(
            () => EvrenTestCaseGeneratorService.ParseTestCases(json));
    }
}
