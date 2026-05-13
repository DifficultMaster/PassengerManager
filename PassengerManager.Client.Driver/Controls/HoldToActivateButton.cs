using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PassengerManager.Client.Driver.Controls
{
    public class HoldToActivateButton : Button
    {
        public static readonly DependencyProperty HoldDurationProperty =
            DependencyProperty.Register(nameof(HoldDuration), typeof(TimeSpan), typeof(HoldToActivateButton),
                new PropertyMetadata(TimeSpan.FromSeconds(3)));

        public static readonly DependencyProperty HoldProgressProperty =
            DependencyProperty.Register(nameof(HoldProgress), typeof(double), typeof(HoldToActivateButton),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty HoldCompletedCommandProperty =
            DependencyProperty.Register(nameof(HoldCompletedCommand), typeof(ICommand), typeof(HoldToActivateButton));

        public TimeSpan HoldDuration
        {
            get => (TimeSpan)GetValue(HoldDurationProperty);
            set => SetValue(HoldDurationProperty, value);
        }

        public double HoldProgress
        {
            get => (double)GetValue(HoldProgressProperty);
            private set => SetValue(HoldProgressProperty, value);
        }

        public ICommand? HoldCompletedCommand
        {
            get => (ICommand?)GetValue(HoldCompletedCommandProperty);
            set => SetValue(HoldCompletedCommandProperty, value);
        }

        private readonly DispatcherTimer _timer;
        private DateTime _holdStart;
        private bool _isHolding;

        public HoldToActivateButton()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timer.Tick += OnTimerTick;
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            e.Handled = true;
            base.OnPreviewMouseLeftButtonDown(e);
            StartHold();
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            e.Handled = true;
            base.OnPreviewMouseLeftButtonUp(e);
            CancelHold();
        }

        protected override void OnPreviewTouchDown(TouchEventArgs e)
        {
            e.Handled = true;
            base.OnPreviewTouchDown(e);
            StartHold();
        }

        protected override void OnPreviewTouchUp(TouchEventArgs e)
        {
            e.Handled = true;
            base.OnPreviewTouchUp(e);
            CancelHold();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            CancelHold();
        }

        private void StartHold()
        {
            if (_isHolding)
            {
                return;
            }

            _isHolding = true;
            _holdStart = DateTime.UtcNow;
            HoldProgress = 0.0;
            _timer.Start();
        }

        private void CancelHold()
        {
            if (!_isHolding)
            {
                return;
            }

            _isHolding = false;
            _timer.Stop();
            HoldProgress = 0.0;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!_isHolding)
            {
                return;
            }

            double elapsed = (DateTime.UtcNow - _holdStart).TotalMilliseconds;
            double duration = HoldDuration.TotalMilliseconds;
            double progress = duration <= 0 ? 1.0 : Math.Min(1.0, elapsed / duration);

            HoldProgress = progress;

            if (progress >= 1.0)
            {
                _timer.Stop();
                _isHolding = false;

                if (HoldCompletedCommand?.CanExecute(null) == true)
                {
                    HoldCompletedCommand.Execute(null);
                }
            }
        }
    }
}
