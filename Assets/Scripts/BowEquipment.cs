using UnityEngine;

// The player's old bow slot. scene2 still carries this component (and EquipBow still fills its two fields), so it
// stays, but the bow is retired: it hands the player over to BatEquipment and switches itself off. Nothing is
// equipped on the left hand any more, and there is no crosshair.
public class BowEquipment : MonoBehaviour {
    [SerializeField] GameObject bowModel;
    [SerializeField] GameObject arrowModel;

    void Awake() {
        if (GetComponent<BatEquipment>() == null) {
            gameObject.AddComponent<BatEquipment>();
        }
        enabled = false;
    }
}
