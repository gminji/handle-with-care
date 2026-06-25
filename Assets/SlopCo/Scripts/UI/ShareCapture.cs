using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SlopCo.UI
{
    /// <summary>
    /// The outbound half of the share loop: grabs the fully-rendered end-of-run card as a PNG into
    /// <c>persistentDataPath/Clips</c> and opens that folder, plus a clipboard copy of the brag text.
    /// Each client captures its OWN screen locally — no netcode. Desktop-targeted (Steam); on mobile/console
    /// <see cref="GUIUtility.systemCopyBuffer"/> / <see cref="Application.OpenURL"/> are no-ops, which is
    /// acceptable. IO failures are surfaced (logged + <see cref="LastError"/>), never silently swallowed.
    /// </summary>
    public sealed class ShareCapture : MonoBehaviour
    {
        /// <summary>Last capture/copy failure message, or null when the last op succeeded.</summary>
        public static string LastError { get; private set; }

        /// <summary>Capture the backbuffer to a timestamped PNG and open the Clips folder;
        /// <paramref name="onDone"/> reports success so the card can flash the right toast.</summary>
        public void CaptureAndOpen(string gradeLetter, Action<bool> onDone)
            => StartCoroutine(CaptureRoutine(gradeLetter, onDone));

        private IEnumerator CaptureRoutine(string gradeLetter, Action<bool> onDone)
        {
            // Wait for the frame to finish so the capture includes the fully-rendered card. The yield must
            // sit OUTSIDE the try/catch below — C# forbids a yield inside a try that has a catch clause.
            yield return new WaitForEndOfFrame();

            bool ok = false;
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture(); // synchronous — avoids the async-file-write race
                byte[] png = tex.EncodeToPNG();
                string dir = Path.Combine(Application.persistentDataPath, "Clips");
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string safe = string.IsNullOrEmpty(gradeLetter) ? "X" : gradeLetter;
                File.WriteAllBytes(Path.Combine(dir, $"slopco_{stamp}_{safe}.png"), png);
                Application.OpenURL("file://" + dir);
                LastError = null;
                ok = true;
            }
            catch (Exception e)
            {
                // Surface, don't swallow: disk/permission failures are real and the player needs the toast.
                LastError = e.Message;
                Debug.LogWarning($"[ShareCapture] screenshot save failed: {e}");
            }
            finally
            {
                if (tex != null) Destroy(tex);
            }

            onDone?.Invoke(ok);
        }

        /// <summary>Copy brag text to the system clipboard. Returns false (and logs) on failure.</summary>
        public static bool CopyToClipboard(string text)
        {
            try
            {
                GUIUtility.systemCopyBuffer = text ?? string.Empty;
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning($"[ShareCapture] clipboard copy failed: {e}");
                return false;
            }
        }
    }
}
