using NUnit.Framework;
using SlopCo.Items;

namespace SlopCo.Tests.EditMode
{
    /// <summary>Verifies the pure inventory-slot transitions (InventoryLogic) without Netcode/UnityEngine.</summary>
    public class InventoryLogicTests
    {
        [Test]
        public void GrantConsumable_IntoEmpty_Succeeds()
        {
            Assert.That(InventoryLogic.CanGrantConsumable(InventoryLogic.Empty), Is.True);
            Assert.That(InventoryLogic.GrantConsumable(InventoryLogic.Empty, 2), Is.EqualTo(2));
        }

        [Test]
        public void GrantConsumable_WhenOccupied_DoesNotOverwrite()
        {
            Assert.That(InventoryLogic.CanGrantConsumable(2), Is.False);
            Assert.That(InventoryLogic.GrantConsumable(2, 5), Is.EqualTo(2)); // keeps existing
        }

        [Test]
        public void DiscardConsumable_EmptiesSlot()
        {
            Assert.That(InventoryLogic.DiscardConsumable(3), Is.EqualTo(InventoryLogic.Empty));
        }

        [Test]
        public void Permanent_AddOwnsAndCount()
        {
            int m = 0;
            m = InventoryLogic.AddPermanent(m, 4);
            m = InventoryLogic.AddPermanent(m, 5);
            Assert.That(InventoryLogic.OwnsPermanent(m, 4), Is.True);
            Assert.That(InventoryLogic.OwnsPermanent(m, 5), Is.True);
            Assert.That(InventoryLogic.OwnsPermanent(m, 6), Is.False);
            Assert.That(InventoryLogic.PermanentCount(m), Is.EqualTo(2));
        }

        [Test]
        public void AddPermanent_OutOfRange_NoChange()
        {
            int m = InventoryLogic.AddPermanent(0, 99);
            Assert.That(m, Is.EqualTo(0));
            Assert.That(InventoryLogic.OwnsPermanent(0, -1), Is.False);
        }

        [Test]
        public void Cycle_NoneOwned_ReturnsEmpty()
        {
            Assert.That(InventoryLogic.CycleSelected(0, InventoryLogic.Empty, 32), Is.EqualTo(InventoryLogic.Empty));
        }

        [Test]
        public void Cycle_SingleOwned_ReturnsSame()
        {
            int m = InventoryLogic.AddPermanent(0, 4);
            Assert.That(InventoryLogic.CycleSelected(m, 4, 32), Is.EqualTo(4));
        }

        [Test]
        public void Cycle_MultiOwned_AdvancesAndWraps()
        {
            int m = 0;
            m = InventoryLogic.AddPermanent(m, 4);
            m = InventoryLogic.AddPermanent(m, 5);
            Assert.That(InventoryLogic.CycleSelected(m, 4, 32), Is.EqualTo(5));
            Assert.That(InventoryLogic.CycleSelected(m, 5, 32), Is.EqualTo(4)); // wrap
        }

        [Test]
        public void Cycle_FromEmpty_PicksFirstOwned()
        {
            int m = InventoryLogic.AddPermanent(0, 5);
            Assert.That(InventoryLogic.CycleSelected(m, InventoryLogic.Empty, 32), Is.EqualTo(5));
        }
    }
}
