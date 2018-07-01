﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Profiling;
using StackExchange.Profiling.Internal;

namespace MiniProfilerXRay.EntityFrameworkCore
{
    /// <summary>
    /// Diagnostic listener for Microsoft.EntityFrameworkCore.* events
    /// </summary>
    public class RelationalDiagnosticListener : IMiniProfilerDiagnosticListener
    {
        // Maps to https://github.com/aspnet/EntityFramework/blob/f386095005e46ea3aa4d677e4439cdac113dbfb1/src/EFCore.Relational/Internal/RelationalDiagnostics.cs
        // See https://github.com/aspnet/EntityFramework/issues/7939 for info

        /// <summary>
        /// Diagnostic Listener name to handle
        /// </summary>
        public string ListenerName => "Microsoft.EntityFrameworkCore";

        // Tracking currently open items, connections, and transactions, for logging upon their completion or error
        private readonly ConcurrentDictionary<Guid, CustomTiming>
            _commands = new ConcurrentDictionary<Guid, CustomTiming>(),
            _opening = new ConcurrentDictionary<Guid, CustomTiming>(),
            _closing = new ConcurrentDictionary<Guid, CustomTiming>();

        // See https://github.com/aspnet/EntityFramework/issues/8007
        private readonly ConcurrentDictionary<Guid, CustomTiming>
            _readers = new ConcurrentDictionary<Guid, CustomTiming>();

        /// <summary>
        /// Notifies the observer that the provider has finished sending push-based notifications.
        /// </summary>
        public void OnCompleted() { }
        /// <summary>
        /// Notifies the observer that the provider has experienced an error condition.
        /// </summary>
        /// <param name="error">An object that provides additional information about the error.</param>
        public void OnError(Exception error) => Trace.WriteLine(error);

        /// <summary>
        /// Provides the observer with new data.
        /// </summary>
        /// <param name="kv">The current notification information.</param>
        public void OnNext(KeyValuePair<string, object> kv)
        {
            var key = kv.Key;
            var val = kv.Value;
            if (key == RelationalEventId.CommandExecuting.Name)
            {
                if (val is CommandEventData data)
                {
                    var timing = data.Command.GetTiming(data.ExecuteMethod + (data.IsAsync ? " (Async)" : null), MiniProfiler.Current);
                    if (timing != null)
                    {
                        timing.CommandString = timing.CommandString + "\n/*XRAY ";
                        timing.CommandString = timing.CommandString + data.Command.Connection.Database + "@" + data.Command.Connection.DataSource;
                        timing.CommandString = timing.CommandString + " */";

                        _commands[data.CommandId] = timing;
                    }
                }
            }
            else if (key == RelationalEventId.CommandExecuted.Name)
            {
                if (val is CommandExecutedEventData data && _commands.TryRemove(data.CommandId, out var current))
                {
                    // A completion for a DataReader only means we *started* getting data back, not finished.
                    if (data.Result is RelationalDataReader reader)
                    {
                        _readers[data.CommandId] = current;
                        current.FirstFetchCompleted();
                    }
                    else
                    {
                        current.Stop();
                    }
                }
            }
            else if (key == RelationalEventId.CommandError.Name)
            {
                if (val is CommandErrorEventData data && _commands.TryRemove(data.CommandId, out var command))
                {
                    command.Errored = true;
                    command.Stop();
                }
            }
            else if (key == RelationalEventId.DataReaderDisposing.Name)
            {
                if (val is DataReaderDisposingEventData data && _readers.TryRemove(data.CommandId, out var reader))
                {
                    reader.Stop();
                }
            }
            // TODO consider switching to ConnectionEndEventData.Duration
            // This isn't as trivial as it appears due to the start offset of the request
            else if (key == RelationalEventId.ConnectionOpening.Name)
            {
                var profiler = MiniProfiler.Current;
                // Track if we either don't have options (assume defaults), or we're set to track.
                if (val is ConnectionEventData data && (profiler?.Options == null || profiler.Options.TrackConnectionOpenClose))
                {
                    var timing = profiler.CustomTiming("sql",
                        data.IsAsync ? "Connection OpenAsync()" : "Connection Open()",
                        data.IsAsync ? "OpenAsync" : "Open");
                    if (timing != null)
                    {
                        timing.CommandString = timing.CommandString + "\n/*XRAY ";
                        timing.CommandString = timing.CommandString + data.Connection.Database + "@" + data.Connection.DataSource;
                        timing.CommandString = timing.CommandString + " */";

                        _opening[data.ConnectionId] = timing;
                    }
                }
            }
            else if (key == RelationalEventId.ConnectionOpened.Name)
            {
                if (val is ConnectionEndEventData data && _opening.TryRemove(data.ConnectionId, out var openingTiming))
                {
                    openingTiming?.Stop();
                }
            }
            else if (key == RelationalEventId.ConnectionClosing.Name)
            {
                var profiler = MiniProfiler.Current;
                // Track if we either don't have options (assume defaults), or we're set to track.
                if (val is ConnectionEventData data && (profiler?.Options == null || profiler.Options.TrackConnectionOpenClose))
                {
                    var timing = profiler.CustomTiming("sql",
                        data.IsAsync ? "Connection CloseAsync()" : "Connection Close()",
                        data.IsAsync ? "CloseAsync" : "Close");
                    if (timing != null)
                    {
                        timing.CommandString = timing.CommandString + "\n/*XRAY ";
                        timing.CommandString = timing.CommandString + data.Connection.Database + "@" + data.Connection.DataSource;
                        timing.CommandString = timing.CommandString + " */";

                        _closing[data.ConnectionId] = timing;
                    }
                }
            }
            else if (key == RelationalEventId.ConnectionClosed.Name)
            {
                if (val is ConnectionEndEventData data && _closing.TryRemove(data.ConnectionId, out var closingTiming))
                {
                    closingTiming?.Stop();
                }
            }
            else if (key == RelationalEventId.ConnectionError.Name)
            {
                if (val is ConnectionErrorEventData data)
                {
                    if (_opening.TryRemove(data.ConnectionId, out var openingTiming))
                    {
                        openingTiming.Errored = true;
                    }
                    if (_closing.TryRemove(data.ConnectionId, out var closingTiming))
                    {
                        closingTiming.Errored = true;
                    }
                }
            }
        }

        // Transactions - Not in yet
        //[DiagnosticName("Microsoft.EntityFrameworkCore.TransactionStarted")]
        //public void OnTransactionStarted()
        //{
        //    // Available: DbConnection connection, Guid connectionId, DbTransaction transaction
        //}

        //[DiagnosticName("Microsoft.EntityFrameworkCore.TransactionCommitted")]
        //public void OnTransactionCommitted()
        //{
        //    // Available: DbConnection connection, Guid connectionId, DbTransaction transaction, long startTimestamp, long currentTimestamp
        //}

        //[DiagnosticName("Microsoft.EntityFrameworkCore.TransactionRolledback")]
        //public void OnTransactionRolledback()
        //{
        //    // Available: DbConnection connection, Guid connectionId, DbTransaction transaction, long startTimestamp, long currentTimestamp
        //}

        //[DiagnosticName("Microsoft.EntityFrameworkCore.TransactionDisposed")]
        //public void OnTransactionDisposed()
        //{
        //    // Available: DbConnection connection, Guid connectionId, DbTransaction transaction
        //}

        //[DiagnosticName("Microsoft.EntityFrameworkCore.TransactionError")]
        //public void OnTransactionError()
        //{
        //    // Available: DbConnection connection, Guid connectionId, DbTransaction transaction, string action, Exception exception, long startTimestamp, long currentTimestamp
        //}
    }
}