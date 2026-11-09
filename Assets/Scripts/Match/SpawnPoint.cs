using UnityEngine;

namespace CageDuel.Match
{
    /// <summary>
    /// Marks a player or bot spawn point inside the arena.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private int spawnIndex;
        [SerializeField] private string spawnLabel = "Spawn";
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.85f, 1.0f, 0.85f);

        public int SpawnIndex => spawnIndex;
        public string SpawnLabel => spawnLabel;

        public void Configure(int index, string label, Color color)
        {
            spawnIndex = index;
            spawnLabel = label;
            gizmoColor = color;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Vector3 pos = transform.position;
            Gizmos.DrawWireSphere(pos + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawLine(pos + Vector3.up * 0.1f, pos + Vector3.up * 0.1f + transform.forward * 1.6f);
            Gizmos.DrawSphere(pos + Vector3.up * 0.1f + transform.forward * 1.6f, 0.1f);
        }
    }
}
