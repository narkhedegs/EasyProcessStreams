[![Build status](https://ci.appveyor.com/api/projects/status/bn0m5c1mx1o9e5x9/branch/master?svg=true)](https://ci.appveyor.com/project/narkhedegs/easyprocessstreams/branch/master)
# Easy Process Streams

Process's standard output, input and error made easy.

<h1 align="center">
  <br>
  <img width="300" src="https://www.iconfinder.com/icons/376113/776060/512/raster?token=1444070703-ReEeWIDgKcWRqhC7HS6XQK23xIug05MA-Ls8eFW/ARb8N3L/Khz3SfWJHoOM%3D">
  <br>
  <br>
</h1>

### Purpose

One of the major but less-obvious problem with System.Diagnostics.Process API is that of deadlocking. All three process streams (in, out, and error) are finite in how much content they can buffer. If the internal buffer fills up, then whoever is writing to the stream will block and our .NET app is also blocked reading to the end of standard out. Thus, we’ve found ourselves in a deadlock.

EasyProcessStreams resolves the deadlok problem by using Tasks to asynchronously read from the streams while the .NET wait for the external executable to exit.

### Requirements

- .NET 4.5 and above

# Installation

EasyProcessStreams is available at [Nuget](https://www.nuget.org/packages/EasyProcessStreams/) and can be installed as a package using VisualStudio NuGet package manager or via the NuGet command line:
> Install-Package EasyProcessStreams

# Usage

```cs
using Narkhedegs;
```

EasyProcessStreams provides two classes, ProcessStreamReader and ProcessStreamWriter, that can be used for wrapping the Standard Output, Standard Error and Standard Input as shown in the code below.

```cs
    /// <summary>
    /// Wrapper around System.Diagnostics.Process. EasyProcess uses EasyProcessStreams to create a Process that can be 
    /// easily piped to other processes without deadlocks.
    /// </summary>
    public sealed class EasyProcess
    {
        /// <summary>
        /// Wrapped System.Diagnostics.Process.
        /// </summary>
        private readonly Process _process;

        private readonly Task<ProcessResult> _task; 

        /// <summary>
        /// Standard Output.
        /// </summary>
        public ProcessStreamReader Output { get; }

        /// <summary>
        /// Standard Error.
        /// </summary>
        public ProcessStreamReader Error { get; }

        /// <summary>
        /// Standard Input.
        /// </summary>
        public ProcessStreamWriter Input { get; }

        /// <summary>
        /// The result of the process including ExitCode, success indicator, Standard Output as string and 
        /// Standard Error as string.
        /// </summary>
        public ProcessResult Result => _task.Result;

        /// <summary>
        /// Initializes a new instance of EasyProcess with the given parameters.
        /// </summary>
        /// <param name="executable">Absolute or relative path of the executable.</param>
        /// <param name="arguments">Arguments for the executable is any.</param>
        public EasyProcess(string executable, params string[] arguments)
        {
            var processStartInformation = new ProcessStartInfo
            {
                Arguments = string.Join(" ", arguments),
                FileName = executable,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            _process = new Process { StartInfo = processStartInformation, EnableRaisingEvents = true };

            var taskCompletionSource = new TaskCompletionSource<bool>();
            _process.Exited += (sender, eventArguments) => { taskCompletionSource.SetResult(true); };
            var processTask = taskCompletionSource.Task;

            _process.Start();

            var inputOutputTasks = new List<Task>(2);

            // Wrap process's Standard Output with ProcessStreamReader.
            Output = new ProcessStreamReader(_process.StandardOutput);
            inputOutputTasks.Add(Output.Task);

            // Wrap process's Standard Error with ProcessStreamReader.
            Error = new ProcessStreamReader(_process.StandardError);
            inputOutputTasks.Add(Error.Task);

            Input = new ProcessStreamWriter(_process.StandardInput);

            _task = CreateCombinedTask(processTask, inputOutputTasks);
        }

        /// <summary>
        /// Combines process task and input output tasks.
        /// </summary>
        /// <param name="processTask">Task that waits for the process to exit. </param>
        /// <param name="inputOutputTasks">Tasks that read Standard Output and Standard Error.</param>
        /// <returns></returns>
        private async Task<ProcessResult> CreateCombinedTask(Task processTask, List<Task> inputOutputTasks)
        {
            int exitCode;
            try
            {
                await processTask.ConfigureAwait(false);
                exitCode = _process.ExitCode;
            }
            finally
            {
                _process.Dispose();
            }

            await Task.WhenAll(inputOutputTasks).ConfigureAwait(false);

            return new ProcessResult(exitCode, this);
        }
    }
```
```cs
    /// <summary>
    /// Represents the result of a process including ExitCode, success indicator, Standard Output as string and 
    /// Standard Error as string.
    /// </summary>
    public sealed class ProcessResult
    {
        private readonly Lazy<string> _standardOutput, _standardError;

        /// <summary>
        /// Initializes a new instance of ProcessResult class.
        /// </summary>
        /// <param name="exitCode">Exit code for the process.</param>
        /// <param name="process">Instance of EasyProcess.</param>
        public ProcessResult(int exitCode, EasyProcess process)
        {
            ExitCode = exitCode;
            _standardOutput = new Lazy<string>(() => process.Output.ReadToEnd());
            _standardError = new Lazy<string>(() => process.Error.ReadToEnd());
        }

        /// <summary>
        /// The exit code of the process.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Returns true if the exit code is 0 (indicating success).
        /// </summary>
        public bool Success => ExitCode == 0;

        /// <summary>
        /// If available, the full standard output text of the command.
        /// </summary>
        public string StandardOutput => _standardOutput.Value;

        /// <summary>
        /// If available, the full standard error text of the command.
        /// </summary>
        public string StandardError => _standardError.Value;
    }
```
```cs
    class Program
    {
        static void Main(string[] args)
        {
            var process1 = new EasyProcess(@"executable1.exe", "arg1", "arg2");
            var process2 = new EasyProcess(@"executable2.exe", "arg1", "arg2");

            // Pipe the output of first process into second process.
            process1.Output.PipeToAsync(process2.Input.BaseStream);

            // Final result.
            var result = process1.Result;

            Console.WriteLine(result.StandardOutput);

            Console.ReadKey();
        }
    }
```

# Credits

All credits goes to [madelson](https://github.com/madelson). This project is just a small part of [MedallionShell](https://github.com/madelson/MedallionShell), with few modifications, published as a separate NuGet package.

# License

MIT © [narkhedegs](https://github.com/narkhedegs)