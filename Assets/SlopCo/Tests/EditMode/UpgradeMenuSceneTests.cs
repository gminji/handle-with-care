using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Scene-wiring net for the title-screen 강화 menu, against the live Bootstrap.unity
    /// (<see cref="ShopLayoutTests"/> pattern — same additive open, same root+path lookup, because both the
    /// panel and its rows are inactive by default and <c>GameObject.Find</c> cannot see them).
    ///
    /// This exists because the button and panel are UnityEvent/serialized-reference wiring: a renamed
    /// method, a cleared <c>upgradePanel</c> field, or a script reference that failed to resolve all fail
    /// SILENTLY at runtime — a dead button and an unopenable menu, not an exception.
    /// </summary>
    public class UpgradeMenuSceneTests
    {
        private Scene _scene;

        [OneTimeSetUp]
        public void OpenScene()
        {
            _scene = EditorSceneManager.OpenScene("Assets/SlopCo/Scenes/Bootstrap.unity", OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.CloseScene(_scene, true);
        }

        private GameObject Find(string rootName, string path)
        {
            foreach (var go in _scene.GetRootGameObjects())
            {
                if (go.name != rootName) continue;
                var found = go.transform.Find(path);
                return found != null ? found.gameObject : null;
            }
            return null;
        }

        [Test]
        public void UpgradesButton_SitsOnTheTitleScreen_AndOpensTheShop()
        {
            var btn = Find("MenuCanvas", "MainMenuPanel/UpgradesBtn");
            Assert.That(btn, Is.Not.Null, "UPGRADES button missing from MainMenuPanel");

            var button = btn.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, "UpgradesBtn has no Button component");
            Assert.That(button.interactable, Is.True);

            // The click must land on UIManager.OpenUpgrades — the one method that gates the shop to the
            // title screen. A rename here is a dead button with no error anywhere.
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("OpenUpgrades"));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.InstanceOf<UIManager>());

            // The label is localized like every other menu button (not a baked English string).
            var label = btn.transform.Find("Text");
            Assert.That(label, Is.Not.Null, "UpgradesBtn has no Text child");
            Assert.That(label.GetComponent<LocalizedText>(), Is.Not.Null, "UpgradesBtn label is not localized");
        }

        [Test]
        public void UpgradesButton_IsYellow_WithLegibleInk()
        {
            UITheme.ResetPalette();
            var btn = Find("MenuCanvas", "MainMenuPanel/UpgradesBtn");
            Assert.That(btn, Is.Not.Null);

            // 이 메뉴의 버튼 색은 노란색 — the scene's serialized colour must stay the theme token, not drift
            // back to the shared grey every other menu button uses.
            var img = btn.GetComponent<Image>();
            Assert.That(img, Is.Not.Null, "UpgradesBtn has no Image to tint");
            AssertColor(img.color, UITheme.UpgradeYellow, "UPGRADES button face");

            var text = btn.transform.Find("Text").GetComponent<Text>();
            AssertColor(text.color, UITheme.UpgradeYellowInk, "UPGRADES button ink");
        }

        [Test]
        public void UpgradePanel_CarriesTheShopComponent_AndIsWiredIntoUIManager()
        {
            var panel = Find("MenuCanvas", "UpgradePanel");
            Assert.That(panel, Is.Not.Null, "UpgradePanel missing from MenuCanvas");

            // A non-null component here also proves the scene's script GUID still resolves — a broken one
            // deserializes as a "missing script" slot rather than throwing.
            Assert.That(panel.GetComponent<UpgradeShopUI>(), Is.Not.Null,
                        "UpgradePanel has no UpgradeShopUI (missing script reference?)");
            Assert.That(panel.activeSelf, Is.False, "UpgradePanel must start hidden — UIManager owns its visibility");

            var ui = Object.FindFirstObjectByType<UIManager>();
            Assert.That(ui, Is.Not.Null, "no UIManager in the scene");
            var field = new SerializedObject(ui).FindProperty("upgradePanel");
            Assert.That(field, Is.Not.Null, "UIManager has no serialized upgradePanel field");
            Assert.That(field.objectReferenceValue, Is.EqualTo(panel),
                        "UIManager.upgradePanel must point at MenuCanvas/UpgradePanel");
        }

        private static void AssertColor(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f), what + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f), what + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f), what + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.01f), what + " (a)");
        }
    }
}
