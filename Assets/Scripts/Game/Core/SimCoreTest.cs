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
        [DllImport("SimCore")] private static extern long sim_get_posx_raw(IntPtr sim);

        private void Start()
        {
            m_sim = sim_create();
        }

        private void Update()
        {
            if (m_sim == IntPtr.Zero) return;

            m_accumulator += Time.deltaTime * 1000f;
            while(m_accumulator >= m_tickIntervalMs)
            {
                sim_tick(m_sim);
                m_accumulator -= m_tickIntervalMs;

                long raw = sim_get_posx_raw(m_sim);
                float posX = raw / 65536f;          // 표시용 변환 (raw / 2^16)
                Debug.Log($"[SimCore] tick = {sim_current_tick(m_sim)}  posX = {posX:F4}");
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
    }
}