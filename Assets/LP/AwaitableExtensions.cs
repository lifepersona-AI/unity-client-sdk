using System;
using UnityEngine;

namespace LP
{
    /// <summary>
    /// Extension methods for Unity's Awaitable to enable fire-and-forget patterns.
    /// </summary>
    public static class AwaitableExtensions
    {
        /// <summary>
        /// Explicitly marks an Awaitable as fire-and-forget, with automatic error logging.
        /// Use this when you intentionally don't want to await an async operation.
        /// </summary>
        /// <param name="task">The Awaitable to forget</param>
        /// <param name="onException">Optional custom exception handler</param>
        public static async void Forget(this Awaitable task, Action<Exception> onException = null)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                if (onException != null)
                {
                    onException(ex);
                }
                else
                {
                    Debug.LogError($"Unhandled exception in fire-and-forget task: {ex}");
                }
            }
        }

        /// <summary>
        /// Explicitly marks an Awaitable with result as fire-and-forget, with automatic error logging.
        /// </summary>
        public static async void Forget<T>(this Awaitable<T> task, Action<Exception> onException = null)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                if (onException != null)
                {
                    onException(ex);
                }
                else
                {
                    Debug.LogError($"Unhandled exception in fire-and-forget task: {ex}");
                }
            }
        }
    }
}
