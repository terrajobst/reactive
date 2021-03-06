﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

#if HAS_WINFORMS

using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Xunit;
using Microsoft.Reactive.Testing;

namespace ReactiveTests.Tests
{
    
    public class ControlSchedulerTest
    {
        [Fact]
        public void Ctor_ArgumentChecking()
        {
            ReactiveAssert.Throws<ArgumentNullException>(() => new ControlScheduler(null));
        }

        [Fact]
        public void Control()
        {
            var lbl = new Label();
            Assert.Same(lbl, new ControlScheduler(lbl).Control);
        }

        [Fact]
        public void Now()
        {
            var res = new ControlScheduler(new Label()).Now - DateTime.Now;
            Assert.True(res.Seconds < 1);
        }

        [Fact]
        public void Schedule_ArgumentChecking()
        {
            var s = new ControlScheduler(new Label());
            ReactiveAssert.Throws<ArgumentNullException>(() => s.Schedule(42, default(Func<IScheduler, int, IDisposable>)));
            ReactiveAssert.Throws<ArgumentNullException>(() => s.Schedule(42, TimeSpan.FromSeconds(1), default(Func<IScheduler, int, IDisposable>)));
            ReactiveAssert.Throws<ArgumentNullException>(() => s.Schedule(42, DateTimeOffset.Now, default(Func<IScheduler, int, IDisposable>)));
        }

        [Fact(Skip="Run Locally")]
        public void Schedule()
        {
            var evt = new ManualResetEvent(false);

            var id = Thread.CurrentThread.ManagedThreadId;

            var lbl = CreateLabel();
            var sch = new ControlScheduler(lbl);
            
            sch.Schedule(() => { lbl.Text = "Okay"; Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId); });
            sch.Schedule(() => { Assert.Equal("Okay", lbl.Text); Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId); evt.Set(); });

            evt.WaitOne();
            Application.Exit();
        }

        [Fact(Skip="Run Locally")]
        public void ScheduleError()
        {
            var evt = new ManualResetEvent(false);

            var ex = new Exception();

            var lbl = CreateLabelWithHandler(e => {
                Assert.Same(ex, e);
                evt.Set();
            });

            var sch = new ControlScheduler(lbl);
            sch.Schedule(() => { throw ex; });

            evt.WaitOne();
            Application.Exit();
        }

        [Fact(Skip="Run Locally")]
        public void ScheduleRelative()
        {
            ScheduleRelative_(TimeSpan.FromSeconds(0.1));
        }

        [Fact(Skip="Run Locally")]
        public void ScheduleRelative_Zero()
        {
            ScheduleRelative_(TimeSpan.Zero);
        }

        private void ScheduleRelative_(TimeSpan delay)
        {
            var evt = new ManualResetEvent(false);

            var id = Thread.CurrentThread.ManagedThreadId;
            
            var lbl = CreateLabel();
            var sch = new ControlScheduler(lbl);

            sch.Schedule(delay, () =>
            {
                lbl.Text = "Okay";
                Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);
                
                sch.Schedule(() =>
                {
                    Assert.Equal("Okay", lbl.Text);
                    Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);
                    evt.Set();
                });
            });

            evt.WaitOne();
            Application.Exit();
        }

        [Fact(Skip="Run Locally")]
        public void ScheduleRelative_Nested()
        {
            var evt = new ManualResetEvent(false);

            var id = Thread.CurrentThread.ManagedThreadId;

            var lbl = CreateLabel();
            var sch = new ControlScheduler(lbl);

            sch.Schedule(TimeSpan.FromSeconds(0.1), () =>
            {
                sch.Schedule(TimeSpan.FromSeconds(0.1), () =>
                {
                    lbl.Text = "Okay";
                    Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);

                    sch.Schedule(() =>
                    {
                        Assert.Equal("Okay", lbl.Text);
                        Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);
                        evt.Set();
                    });
                });
            });

            evt.WaitOne();
            Application.Exit();
        }

        [Fact(Skip="Run Locally")]
        public void ScheduleRelative_Cancel()
        {
            var evt = new ManualResetEvent(false);

            var id = Thread.CurrentThread.ManagedThreadId;

            var lbl = CreateLabel();
            var sch = new ControlScheduler(lbl);

            sch.Schedule(TimeSpan.FromSeconds(0.1), () =>
            {
                lbl.Text = "Okay";
                Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);

                var d = sch.Schedule(TimeSpan.FromSeconds(0.1), () =>
                {
                    lbl.Text = "Oops!";
                });

                sch.Schedule(() =>
                {
                    d.Dispose();
                });

                sch.Schedule(TimeSpan.FromSeconds(0.2), () =>
                {
                    Assert.Equal("Okay", lbl.Text);
                    Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);
                    evt.Set();
                });
            });

            evt.WaitOne();
            Application.Exit();
        }

        [Fact]
        public void SchedulePeriodic_ArgumentChecking()
        {
            var s = new ControlScheduler(new Label());
            ReactiveAssert.Throws<ArgumentNullException>(() => s.SchedulePeriodic(42, TimeSpan.FromSeconds(1), default(Func<int, int>)));
            ReactiveAssert.Throws<ArgumentOutOfRangeException>(() => s.SchedulePeriodic(42, TimeSpan.Zero, x => x));
            ReactiveAssert.Throws<ArgumentOutOfRangeException>(() => s.SchedulePeriodic(42, TimeSpan.FromMilliseconds(1).Subtract(TimeSpan.FromTicks(1)), x => x));
        }

        [Fact(Skip="Run Locally")]
        public void SchedulePeriodic()
        {
            var evt = new ManualResetEvent(false);

            var id = Thread.CurrentThread.ManagedThreadId;

            var lbl = CreateLabel();
            var sch = new ControlScheduler(lbl);

            var d = new SingleAssignmentDisposable();

            d.Disposable = sch.SchedulePeriodic(1, TimeSpan.FromSeconds(0.1), n =>
            {
                lbl.Text = "Okay " + n;
                Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);

                if (n == 3)
                {
                    d.Dispose();

                    sch.Schedule(TimeSpan.FromSeconds(0.2), () =>
                    {
                        Assert.Equal("Okay 3", lbl.Text);
                        Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);
                        evt.Set();
                    });
                }

                if (n > 3)
                {
                    Assert.True(false);
                }

                return n + 1;
            });

            evt.WaitOne();
            Application.Exit();
        }

        [Fact(Skip="Run Locally")]
        public void SchedulePeriodic_Nested()
        {
            var evt = new ManualResetEvent(false);

            var id = Thread.CurrentThread.ManagedThreadId;

            var lbl = CreateLabel();
            var sch = new ControlScheduler(lbl);

            sch.Schedule(() =>
            {
                lbl.Text = "Okay";

                var d = new SingleAssignmentDisposable();

                d.Disposable = sch.SchedulePeriodic(1, TimeSpan.FromSeconds(0.1), n =>
                {
                    lbl.Text = "Okay " + n;
                    Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);

                    if (n == 3)
                    {
                        d.Dispose();

                        sch.Schedule(TimeSpan.FromSeconds(0.2), () =>
                        {
                            Assert.Equal("Okay 3", lbl.Text);
                            Assert.NotEqual(id, Thread.CurrentThread.ManagedThreadId);
                            evt.Set();
                        });
                    }

                    return n + 1;
                });
            });

            evt.WaitOne();
            Application.Exit();
        }

        private Label CreateLabel()
        {
            var loaded = new ManualResetEvent(false);
            var lbl = default(Label);

            var t = new Thread(() =>
            {
                lbl = new Label();
                var frm = new Form { Controls = { lbl }, Width = 0, Height = 0, FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false };
                frm.Load += (_, __) =>
                {
                    loaded.Set();
                };
                Application.Run(frm);
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            loaded.WaitOne();
            return lbl;
        }

        private Label CreateLabelWithHandler(Action<Exception> handler)
        {
            var loaded = new ManualResetEvent(false);
            var lbl = default(Label);

            var t = new Thread(() =>
            {
                lbl = new Label();
                var frm = new Form { Controls = { lbl }, Width = 0, Height = 0, FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false };
                frm.Load += (_, __) =>
                {
                    loaded.Set();
                };
                Application.ThreadException += (o, e) =>
                {
                    handler(e.Exception);
                };
                Application.Run(frm);
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();

            loaded.WaitOne();
            return lbl;
        }
    }
}
#endif