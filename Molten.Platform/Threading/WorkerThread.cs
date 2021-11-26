﻿using Molten.Collections;
using System;
using System.Threading;

namespace Molten.Threading
{
    internal class WorkerThread
    {
        ThreadedQueue<IWorkerTask> _queue;
        AutoResetEvent _reset;
        Thread _thread;
        bool _shouldExit;

        internal WorkerThread(string name, ThreadedQueue<IWorkerTask> taskQueue)
        {
            _reset = new AutoResetEvent(false);
            _queue = taskQueue;

            _thread = new Thread(() =>
            {
                IWorkerTask task = null;

                while (!_shouldExit)
                {
                    if (_queue != null && _queue.TryDequeue(out task))
                        task.Run();
                    else
                        _reset.WaitOne();
                }
            });

            _thread.Name = name;
        }

        internal void Wake()
        {
            _reset.Set();
        }

        internal void Start()
        {
            _thread.Start();
        }

        internal void Abort()
        {
            _thread.Abort();
        }

        internal void Exit()
        {
            _shouldExit = true;
            Wake();
        }


        public string Name => _thread.Name;
    }
}
