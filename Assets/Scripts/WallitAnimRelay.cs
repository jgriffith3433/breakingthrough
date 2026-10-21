using UnityEngine;

// Forwards OnBatContact animation events from Wallit's child Animator GameObject to BatEquipment on the Player root.
public class WallitAnimRelay : MonoBehaviour {
    public void OnBatContact() {
        if (BatEquipment.Instance != null && transform.IsChildOf(BatEquipment.Instance.transform)) {
            BatEquipment.Instance.OnBatContact();
        }
    }
}
