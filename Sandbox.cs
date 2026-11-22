using System;
using UnityEngine;

namespace CSSaveSlots
{
    /// <summary>沙盒模式（教程里的 sandbox 场地，可在里面随便用控制台）检测。</summary>
    internal static class Sandbox
    {
        /// <summary>是否处于沙盒教程场地。</summary>
        public static bool IsActive
        {
            get
            {
                try
                {
                    var t = typeof(TutorialHandler);
                    var f = t.GetField("main", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var handler = f != null ? f.GetValue(null) as UnityEngine.Object : null;
                    if (handler == null) return false;

                    var courseField = t.GetField("activeCourse", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (courseField == null) return false;
                    var course = courseField.GetValue(handler) as TutorialCourse;
                    if (course == null) return false;

                    string name = course.LocaleName();
                    return !string.IsNullOrEmpty(name) && name.IndexOf("sandbox", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch (Exception e)
                {
                    Log.Warn("沙盒检测失败：" + e.Message);
                    return false;
                }
            }
        }
    }

    /// <summary>游戏内提示：控制台回显 + 日志（不弹突兀的窗口）。</summary>
    internal static class Ui
    {
        public static void Notify(string msg)
        {
            Log.Info(msg);
            try
            {
                var t = typeof(ConsoleScript);
                object inst = t.GetField("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null);
                if (inst == null) return;
                var m = t.GetMethod("LogToConsole", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(string) }, null);
                m?.Invoke(inst, new object[] { msg });
            }
            catch { /* 回显失败无所谓 */ }
        }

        /// <summary>原版左上角小提示（DoAlert 的第二参为 false 时不占屏幕中央）。</summary>
        public static void Alert(string msg, bool important = false)
        {
            Log.Info(msg);
            try
            {
                if (PlayerCamera.main != null) PlayerCamera.main.DoAlert(msg, important);
            }
            catch { /* ignore */ }
        }
    }
}
