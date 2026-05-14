using NSubstitute;

namespace DynamicControls.Core.Tests.Infrastructure;

public class LoggerTests
{
    private readonly IFileSystem _fs = TestFs.Create();
    private const string RootDir = @"C:\plugin";
    private static readonly string LogDir = Path.Combine(RootDir, "Logs");
    private static readonly string LogPath = Path.Combine(RootDir, "Logs", "debug.log");
    private static readonly DateTime FixedTime = new(2026, 6, 18, 14, 30, 45);

    private Logger CreateLogger(bool debugEnabled = false) =>
        new(_fs, RootDir, () => FixedTime) { IsDebugEnabled = debugEnabled };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Info_AlwaysAppendsFormattedLineToLogFile(bool debugEnabled)
    {
        // given a logger with the debug flag in either state, and the log dir already exists
        _fs.DirectoryExists(LogDir).Returns(true);
        Logger underTest = CreateLogger(debugEnabled: debugEnabled);

        // when an info message is logged
        underTest.Info("starting up");

        // then a formatted line is appended at the log path regardless of the debug flag
        _fs.Received(1).AppendAllText(LogPath, "[14:30:45] [INFO] starting up\r\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Error_AlwaysAppendsFormattedLineToLogFile(bool debugEnabled)
    {
        // given a logger with the debug flag in either state, and the log dir already exists
        _fs.DirectoryExists(LogDir).Returns(true);
        Logger underTest = CreateLogger(debugEnabled: debugEnabled);

        // when an error message is logged
        underTest.Error("something broke");

        // then a formatted line is appended at the log path regardless of the debug flag
        _fs.Received(1).AppendAllText(LogPath, "[14:30:45] [ERROR] something broke\r\n");
    }

    [Fact]
    public void Debug_DebugDisabled_WritesNothing()
    {
        // given a logger with debug disabled
        Logger underTest = CreateLogger(debugEnabled: false);

        // when a debug message is logged
        underTest.Debug("detail");

        // then no append occurs
        _fs.DidNotReceive().AppendAllText(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void Debug_DebugEnabled_AppendsFormattedLineToLogFile()
    {
        // given a logger with debug enabled, and the log dir already exists
        _fs.DirectoryExists(LogDir).Returns(true);
        Logger underTest = CreateLogger(debugEnabled: true);

        // when a debug message is logged
        underTest.Debug("detail");

        // then a formatted line is appended at the log path
        _fs.Received(1).AppendAllText(LogPath, "[14:30:45] [DEBUG] detail\r\n");
    }

    [Fact]
    public void Write_DirectoryMissing_CreatesDirectoryBeforeAppending()
    {
        // given the log directory does not yet exist
        _fs.DirectoryExists(LogDir).Returns(false);
        Logger underTest = CreateLogger();

        // when a message is logged
        underTest.Info("first write");

        // then the directory is created before the file is written
        Received.InOrder(() =>
        {
            _fs.CreateDirectory(LogDir);
            _fs.AppendAllText(LogPath, Arg.Any<string>());
        });
    }

    [Fact]
    public void Write_DirectoryExists_DoesNotCreateDirectory()
    {
        // given the log directory already exists
        _fs.DirectoryExists(LogDir).Returns(true);
        Logger underTest = CreateLogger();

        // when a message is logged
        underTest.Info("message");

        // then no directory creation is attempted
        _fs.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [Fact]
    public void ClearLog_LogFileExists_DeletesLogFile()
    {
        // given a logger and an existing log file
        _fs.FileExists(LogPath).Returns(true);
        Logger underTest = CreateLogger();

        // when ClearLog is called
        underTest.ClearLog();

        // then the log file is deleted
        _fs.Received(1).DeleteFile(LogPath);
    }

    [Fact]
    public void ClearLog_NoLogFile_DoesNothing()
    {
        // given a logger and no existing log file (fresh install)
        _fs.FileExists(LogPath).Returns(false);
        Logger underTest = CreateLogger();

        // when ClearLog is called
        underTest.ClearLog();

        // then no deletion is attempted
        _fs.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public void Constructor_OmittedClock_DefaultsToWallClock()
    {
        // given a logger constructed without an explicit clock
        _fs.DirectoryExists(LogDir).Returns(true);
        var underTest = new Logger(_fs, RootDir);

        // when an info message is logged
        underTest.Info("hi");

        // then a line is written; we don't pin the timestamp, only that the message
        // and level are preserved at the end of the formatted line
        _fs.Received(1).AppendAllText(LogPath, Arg.Is<string>(s => s.EndsWith("[INFO] hi\r\n")));
    }
}
