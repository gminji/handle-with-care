using NUnit.Framework;
using SlopCo.Items;

namespace SlopCo.Tests.EditMode
{
    /// <summary>Verifies the pure teammate-loadout display selection (LoadoutView) without Netcode/UnityEngine.</summary>
    public class LoadoutViewTests
    {
        [Test]
        public void AugmentDisplayId_ValidPermanent_ReturnsId()
        {
            // 4 and 5 are the permanent ids in ItemCatalog.
            Assert.That(LoadoutView.AugmentDisplayId(4), Is.EqualTo(4));
            Assert.That(LoadoutView.AugmentDisplayId(5), Is.EqualTo(5));
        }

        [Test]
        public void AugmentDisplayId_EmptyOrInvalid_ReturnsEmpty()
        {
            Assert.That(LoadoutView.AugmentDisplayId(InventoryLogic.Empty), Is.EqualTo(InventoryLogic.Empty));
            Assert.That(LoadoutView.AugmentDisplayId(99), Is.EqualTo(InventoryLogic.Empty));
        }

        [Test]
        public void ItemDisplayId_HeldConsumable_TakesPriority()
        {
            // holding 2, last used 4 → show the held one (2)
            Assert.That(LoadoutView.ItemDisplayId(2, 4), Is.EqualTo(2));
        }

        [Test]
        public void ItemDisplayId_NoHeld_FallsBackToLastUsed()
        {
            Assert.That(LoadoutView.ItemDisplayId(InventoryLogic.Empty, 4), Is.EqualTo(4));
        }

        [Test]
        public void ItemDisplayId_NeitherValid_ReturnsEmpty()
        {
            Assert.That(LoadoutView.ItemDisplayId(InventoryLogic.Empty, InventoryLogic.Empty),
                Is.EqualTo(InventoryLogic.Empty));
            Assert.That(LoadoutView.ItemDisplayId(-5, 99), Is.EqualTo(InventoryLogic.Empty));
        }

        [Test]
        public void ItemIsHeld_ReflectsConsumableValidity()
        {
            Assert.That(LoadoutView.ItemIsHeld(0), Is.True);
            Assert.That(LoadoutView.ItemIsHeld(InventoryLogic.Empty), Is.False);
            Assert.That(LoadoutView.ItemIsHeld(99), Is.False);
        }
    }
}
