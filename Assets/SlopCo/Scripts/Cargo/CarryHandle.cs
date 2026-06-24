using UnityEngine;

namespace SlopCo.Cargo
{
    /// <summary>
    /// A grab point on a cargo item. A OneHand item has one handle; a TwoPerson item has two. Plain
    /// component (no networking) — occupancy is tracked authoritatively by the owning CargoItem.
    /// Place these as child empties on the cargo prefab at the spots players should grip.
    /// </summary>
    public sealed class CarryHandle : MonoBehaviour
    {
        [SerializeField] private int handleId = 0;

        public int HandleId => handleId;
        public Transform AttachPoint => transform;
        public CargoItem Owner { get; private set; }

        private void Awake() => Owner = GetComponentInParent<CargoItem>();
    }
}
