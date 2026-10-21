using UnityEngine;

// Attached to Slice's Animator child so AnimationEvents on stab and slash reach Musician on PianoRecital.
public class SliceAnimRelay : MonoBehaviour {
    public void OnSliceKnifeHit() {
        Musician m = GetComponentInParent<Musician>();
        if (m != null) {
            m.OnSliceKnifeHit();
        }
    }
}
