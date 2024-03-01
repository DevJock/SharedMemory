using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class MemoryMapTest : MonoBehaviour
{
    private float m_count = 0;

    [SerializeField]
    private MeshFilter m_meshFilter;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MMVector
    {
        public float x, y, z;
    }

    // Import C library functions
    [DllImport("libc", SetLastError = true)]
    private static extern int shm_open(string name, int oflag, int mode);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr mmap(IntPtr addr, int length, int prot, int flags, int fd, int offset);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int munmap(IntPtr addr, int length);

    // Constants for C library
    private const int O_RDWR = 0x0002;   // Read/write mode
    private const int O_RDONLY = 0x0000;   // Read/write mode
    private const int PROT_READ = 0x01;  // Pages may be read
    private const int PROT_WRITE = 0x02; // Pages may be written
    private const int MAP_SHARED = 0x01; // Share this mapping with all other processes
    private const int MAP_FAILED = -1;

    private Mesh m_mesh;
    private IntPtr mappedMemory;
    private int fd;
    private Process m_process;
    private Vector3[] m_vertices;

    static readonly int structSize = Marshal.SizeOf(typeof(MMVector));
    static readonly int numElements = 393218;
    static readonly int size = structSize * numElements;

    private void StartCPP()
    {
        var fileName = "MemoryMap_macOS";
        var path = Path.Combine(Application.streamingAssetsPath, fileName);
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true // Use the operating system shell to start the process
        };

        m_process = new Process();
        m_process.StartInfo = startInfo;
        m_process.Start();
    }

    private void Awake()
    {
        StartCPP();
    }

    private void Start()
    {
        const string mapName = "SharedMemory";

        // Open the existing shared memory object
        fd = shm_open(mapName, O_RDONLY, 0);
        if (fd == -1)
        {
            Console.WriteLine("Error opening shared memory: " + Marshal.GetLastWin32Error());
            return;
        }

        // Map the shared memory object into the process's address space
        mappedMemory = mmap(IntPtr.Zero, size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
        if (mappedMemory.ToInt64() == MAP_FAILED)
        {
            Console.WriteLine("Error mapping shared memory: " + Marshal.GetLastWin32Error());
            Close();
            return;
        }

        m_meshFilter = GetComponent<MeshFilter>();
        var originalVertices = new List<Vector3>();
        m_mesh = m_meshFilter.mesh;
        m_mesh.GetVertices(originalVertices);
        m_vertices = new Vector3[originalVertices.Count];

        // Cast the shared memory pointer to the desired type using unsafe code
        unsafe
        {
            var sharedDataArray = (MMVector*)mappedMemory;
            for (var i = 0; i < numElements; i++)
            {
                sharedDataArray[i].x = originalVertices[i].x;
                sharedDataArray[i].y = originalVertices[i].y;
                sharedDataArray[i].z = originalVertices[i].z;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        m_count += Time.deltaTime;
        // Every 16 ms (1/60th of a frame)
        if (m_count > 0.016666f)
        {
            unsafe
            {
                var sharedDataArray = (MMVector*)mappedMemory;
                for (var i = 0; i < numElements; i++)
                {
                    m_vertices[i].x = sharedDataArray[i].x;
                    m_vertices[i].y = sharedDataArray[i].y;
                    m_vertices[i].z = sharedDataArray[i].z;
                }

                m_mesh.SetVertices(m_vertices);
                m_mesh.RecalculateBounds();
                m_mesh.RecalculateNormals();
                m_count = 0;
            }
        }
    }
    ~MemoryMapTest()
    {
        Close();
    }

    private void Close()
    {
        // Clean up
        munmap(mappedMemory, size);
        close(fd);
    }

    void OnApplicationQuit()
    {
        Close();
        // Terminate the process when the application quits
        if (m_process != null && !m_process.HasExited)
        {
            m_process.Kill();
            m_process.Dispose();
        }
    }

    private void OnDestroy()
    {
        Close();
    }
}