using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;

namespace ProviderOptionsRepro
{
    internal class Repro
    {
        static void Main(string[] args)
        {
            Run(workAround: true);
            Console.WriteLine("Press a key to continue");
            Console.ReadKey();

            Run(workAround: false);
            Console.WriteLine("Press a key to stop");
            Console.ReadKey();
        }
        public static void Run(bool workAround)
        {
            Console.WriteLine("");
            Console.WriteLine($"Running {nameof(Repro)} with {nameof(Repro)}={workAround}");
            var eventSourceName = EventSource.GetName(typeof(ReproEventSource));
            var sessionName = eventSourceName;
            Console.WriteLine("Creating a '{0}' session", sessionName);
            using (var session = new TraceEventSession(sessionName))
            {
                bool isProcessing = false;
                Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => { session.Dispose(); };

                // Write events received and stop on StopEvent
                session.Source.Dynamic.All += (TraceEvent data) =>
                {
                    Console.WriteLine($"RECEIVED event {data.ID}");
                    if ((ushort)data.ID == ReproEventSource.Events.StopEvent)
                    {
                        Console.WriteLine($"End of {nameof(Repro)} - disposing session");
                        session.Dispose();
                    }
                };

                void EnableWithoutOptions()
                {
                    Console.WriteLine("Enabling provider WITHOUT options");
                    session.EnableProvider(eventSourceName);
                    Console.WriteLine("Provider enabled WITHOUT options");
                }

                void LoadEventSource()
                {
                    var dummy = ReproEventSource.Log;
                }

                void EnableWithOptions()
                {
                    Console.WriteLine("Enabling provider WITH options");
                    var options = new TraceEventProviderOptions();
                    options.EventIDsToEnable =
                        new int[] {
                            ReproEventSource.Events.Event1,
                            ReproEventSource.Events.StopEvent
                        };
                    session.EnableProvider(eventSourceName, options: options);
                    Console.WriteLine("Provider enabled WITH options");
                }

                void GenerateEvents()
                {
                    Task.Factory.StartNew(() =>
                    {
                        Console.WriteLine($"WRITING event {ReproEventSource.Events.Event1}");
                        ReproEventSource.Log.Event1();
                        Console.WriteLine($"WRITING event {ReproEventSource.Events.Event2}");
                        ReproEventSource.Log.Event2();
                        Console.WriteLine($"WRITING event {ReproEventSource.Events.StopEvent}");
                        ReproEventSource.Log.StopEvent();
                    });
                }

                void WaitALittle(TimeSpan timeSpan, string message)
                {
                    Console.WriteLine($"WAITING {timeSpan.ToString()} - {message}");
                    Thread.Sleep(timeSpan);
                    Console.WriteLine($"END WAITING {timeSpan.ToString()} - {message}");
                }

                void EnsureProcessing()
                {
                    if (!isProcessing)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            WaitALittle(TimeSpan.FromMilliseconds(100), "before processing");
                            Console.WriteLine("Starting processing");
                            isProcessing = true;
                            session.Source.Process();
                        });
                    }
                }

                if (workAround)
                {
                    LoadEventSource();
                    EnableWithoutOptions();
                    EnsureProcessing();
                    WaitALittle(TimeSpan.FromSeconds(4), "processing warmup");
                }

                EnableWithOptions();
                EnsureProcessing();
                WaitALittle(TimeSpan.FromSeconds(4), "before generating events");
                GenerateEvents();
                WaitALittle(TimeSpan.FromSeconds(4), "processing events");
            }
        }
    }


    [EventSource(Name = "Observito-Trace-Repro")]
    public sealed class ReproEventSource : EventSource
    {
        public static readonly ReproEventSource Log = new ReproEventSource();

        public sealed class Events
        {
            public const int Event1 = 1;
            public const int Event2 = 2;
            public const int StopEvent = 3;
        }

        public sealed class Keywords
        {
            public const EventKeywords Keyword1 = (EventKeywords)(1 << 0);
        }

        [Event(Events.Event1, Keywords = Keywords.Keyword1)]
        public void Event1() { WriteEvent(Events.Event1); }

        [Event(Events.Event2)]
        public void Event2() { WriteEvent(Events.Event2); }

        [Event(Events.StopEvent)]
        public void StopEvent() { WriteEvent(Events.StopEvent); }
    }

}
