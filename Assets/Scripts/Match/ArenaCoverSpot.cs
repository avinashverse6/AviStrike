using UnityEngine;

namespace CageDuel.Match
{
    /// <summary>
    /// Marks a tactical chest-high cover obstacle inside the arena.
    /// Used for level layout visualization in Phase 1 and AI cover selection in Phase 2.
    /// </summary>
    public class ArenaCoverSpot : MonoBehaviour
    {
        [SerializeField] private float coverHeight = 1.25f;
        [SerializeField] private float standoffDistance = 1.15f;

        public float CoverHeight => coverHeight;
        public float StandoffDistance => standoffDistance;

        public void Configure(float height, float standoff)
        {
            coverHeight = height;
            standoffDistance = standoff;
        }

        /// <summary>
        /// Returns a world-space position tucked behind this cover obstacle opposite a threat position.
        /// </summary>
        public Vector3 GetCoverPositionAwayFrom(Vector3 threatWorldPosition)
        {
            Vector3 flatAway = transform.position - threatWorldPosition;
            flatAway.y = 0f;
            if (flatAway.sqrMagnitude < 0.001f)
            {
                flatAway = -transform.forward;
            }
            else
            {
                flatAway.Normalize();
            }

            Vector3 candidate = transform.position + flatAway * standoffDistance;
            candidate.y = transform.position.y;
            return candidate;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1.0f, 0.75f, 0.15f, 0.7f);
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * (coverHeight * 0.5f),
                new Vector3(2.4f, coverHeight, 0.6f));
        }
    }
}
