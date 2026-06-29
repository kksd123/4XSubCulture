using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Core
{
    public class SimCoreTest : MonoBehaviour
    {
        [DllImport("SimCore")] private static extern IntPtr sim_create();
        [DllImport("SimCore")] private static extern void sim_destroy(IntPtr sim);
        [DllImport("SimCore")] private static extern void sim_tick(IntPtr sim);
        [DllImport("SimCore")] private static extern ulong sim_current_tick(IntPtr sim);
        [DllImport("SimCore")] private static extern ulong sim_spawn(IntPtr sim, long posxRaw, long velxRaw);
        [DllImport("SimCore")] private static extern int sim_unit_count(IntPtr sim);
        [DllImport("SimCore")] private static extern ulong sim_get_unit_simid(IntPtr sim, int index);
        [DllImport("SimCore")] private static extern long sim_get_unit_posx_raw(IntPtr sim, int index);

        private void Start()
        {
            m_sim = sim_create();

            sim_spawn(m_sim, 0, ONE / 4);
            sim_spawn(m_sim, ONE * 10, ONE / 2);
            sim_spawn(m_sim, ONE * 20, -ONE / 10);
        }

        private void Update()
        {
            if (m_sim == IntPtr.Zero) return;

            m_accumulator += Time.deltaTime * 1000f;
            while(m_accumulator >= m_tickIntervalMs)
            {
                sim_tick(m_sim);
                m_accumulator -= m_tickIntervalMs;

                ulong tick = sim_current_tick(m_sim);
                int n = sim_unit_count(m_sim);
                for (int i = 0; i < n; i++)
                {
                    ulong id = sim_get_unit_simid(m_sim, i);
                    float posX = sim_get_unit_posx_raw(m_sim, i) / 65536f;
                    Debug.Log($"[SimCore] tick={tick} unit#{id} posX={posX:F4}");
                }
            }
        }

        private void OnDestroy()
        {
            if(m_sim != IntPtr.Zero)
            {
                sim_destroy(m_sim);
                m_sim = IntPtr.Zero;
            }
        }

        [SerializeField]
        private float m_tickIntervalMs = 100f;

        private IntPtr m_sim;
        private float m_accumulator;

        const long ONE = 65536;
    }
}