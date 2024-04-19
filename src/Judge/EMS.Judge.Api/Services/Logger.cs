using Core.ConventionalServices;
using Core.Services;
using Core.Domain.AggregateRoots.Manager;
using System;
using System.IO;
using Core.Application.Http;
using System.Text;
using System.Collections.Generic;

namespace EMS.Judge.Api.Services
{
    public class Logger : ILogger
    {
        private const string FILE_NAME = "error.log";
        
        private readonly IFileService fileService;
        public Logger(IFileService fileService)
        {
            this.fileService = fileService;
        }

        public void LogError(Exception exception, string message = "")
        {
            var now = DateTime.Now;
            var formatted = 
                $"{now}: " + message + exception.Message + Environment.NewLine
                + exception.StackTrace + Environment.NewLine;
            
            this.Log(message);
        }

        public void LogClientError(string functionality, IEnumerable<Error> errors)
        {
            var now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine($"{now}: {functionality}:");
            foreach (var error in errors)
            {
                sb.AppendLine(error.Message);
                sb.AppendLine(error.StackTrace);
            }
            var filename = $"T{now:HH-mm-ss}_client-error.log";
            this.Log(sb.ToString(), filename);
        }

        public void LogEventError(Exception exception, WitnessEvent witnessEvent)
        {
            var now = DateTime.Now;
            var message =
                $"{now}: Error while executing Witness Event" + Environment.NewLine +
                $"Event: {witnessEvent.Type}" + Environment.NewLine +
                $"TagId: {witnessEvent.TagId}" + Environment.NewLine +
                $"TimeStamp: {witnessEvent.Time}" + Environment.NewLine +
                exception.Message + Environment.NewLine +
                exception.StackTrace + Environment.NewLine;

            this.Log(message);
        }

        private void Log(string message, string filename = FILE_NAME)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "logs", filename);
            Directory.CreateDirectory(path);
            this.fileService.Append(path, message);
        }
    }
}

public interface ILogger : ITransientService
{
    void LogError(Exception exception, string message = "");
    void LogClientError(string functionality, IEnumerable<Error> errors);
    void LogEventError(Exception exception, WitnessEvent witnessEvent);
}
