﻿using Endurance.Judge.Gateways.API.Models;
using EnduranceJudge.Core.ConventionalServices;
using EnduranceJudge.Core.Services;
using System;
using System.IO;

namespace Endurance.Judge.Gateways.API.Services
{
    public class Logger : ILogger
    {
        private const string FILE_NAME = "error.log";
        
        private readonly IFileService fileService;
        public Logger(IFileService fileService)
        {
            this.fileService = fileService;
        }
        
        public void LogEventError(Exception exception, JudgeEvent judgeEvent)
        {
            var now = DateTime.Now;
            var message =
                $"{now}: Error while executing Witness Event" + Environment.NewLine +
                $"Event: {judgeEvent.Type}" + Environment.NewLine +
                $"TagId: {judgeEvent.TagId}" + Environment.NewLine +
                $"TimeStamp: {judgeEvent.Time}" + Environment.NewLine +
                exception.Message + Environment.NewLine +
                exception.StackTrace;

            var path = Path.Combine(Directory.GetCurrentDirectory(), FILE_NAME);
            this.fileService.Append(path, message);
        }
    }

    public interface ILogger : ITransientService
    {
        void LogEventError(Exception exception, JudgeEvent judgeEvent);
    }
}
