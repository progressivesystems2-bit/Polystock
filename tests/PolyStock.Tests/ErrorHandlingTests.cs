using System;
using System.IO;
using PolyStock.WPF.Services;
using Xunit;

namespace PolyStock.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public void LogException_WritesFormattedExceptionDetailsToFile()
    {
        var testInnerEx = new InvalidOperationException("Inner database connection timeout");
        var testEx = new Exception("Primary operation failed to save invoice", testInnerEx);

        ErrorHandlingService.LogException("Unit Test Context", testEx);

        string logPath = ErrorHandlingService.LogFilePath;
        Assert.True(File.Exists(logPath));

        string content = File.ReadAllText(logPath);
        Assert.Contains("Primary operation failed to save invoice", content);
        Assert.Contains("Inner database connection timeout", content);
        Assert.Contains("Unit Test Context", content);
    }
}
