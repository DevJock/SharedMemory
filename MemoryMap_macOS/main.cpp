#include <iostream>
#include <algorithm>
#include <sys/mman.h>
#include <unistd.h>
#include <cstring>
#include <thread>
#include <sys/file.h>

struct MMVector
{
    float x;
    float y;
    float z;
};

int main() {
    const char* mapName = "SharedMemory";
    const int blocks = 393218;
    const int structSize = sizeof(MMVector);
    const long int size =  structSize * blocks; // Get the system page size
    shm_unlink(mapName);

    // Create or open a shared memory region
    int fd = shm_open(mapName, O_CREAT | O_CLOEXEC | O_WRONLY, 0666);
    if (fd == -1) {
        std::cerr << "Error creating/opening shared memory: " << strerror(errno) << std::endl;
        return 1;
    }

    // Resize shared memory region
    if (ftruncate(fd, size) == -1) {
        std::cerr << "Error resizing shared memory: " << strerror(errno) << std::endl;
        close(fd);
        shm_unlink(mapName);
        return 1;
    }

    // Map the shared memory region into the process's address space
    void* mappedMemory = mmap(NULL, size, PROT_WRITE, MAP_SHARED, fd, 0);
    if (mappedMemory == MAP_FAILED) {
        std::cerr << "Error mapping shared memory: " << strerror(errno) << std::endl;
        close(fd);
        shm_unlink(mapName);
        return 1;
    }

    std::cout << "FD: " << fd << std::endl;
    std::cout << "MM: " << mappedMemory << std::endl;
    std::cout << "Opened Memory Map: " << mapName << std::endl;
    std::cout << "Struct Size: " << structSize << std::endl;
    std::cout << "Chunk Size: " << size << std::endl;

    // Now you can use the memory as needed
    auto pData = static_cast<MMVector*>(mappedMemory);

    for (int i=0;i < blocks;i++)
    {
        pData[i].x = 0.0f;
        pData[i].y = 0.0f;
        pData[i].z = 0.0f;
    }

    bool exitRequested = false;
    static int exitCounter = 0;
    float amount = 0.001f;

    while (!exitRequested)
    {
        // Update the content
        for (int i=0;i < blocks;i++) {
            if ((pData[i].x + pData[i].y + pData[i].z) != 0) {
                pData[i].x = pData[i].x + (pData[i].x * amount);
                pData[i].y = pData[i].y + (pData[i].y * amount);
                pData[i].z = pData[i].z + (pData[i].z * amount);
            }
        }

        // Sleep for a short duration
        std::this_thread::sleep_for(std::chrono::milliseconds(16));

        if (++exitCounter >= 60000000)
        {
            exitRequested = true;
        }
    }

    // Clean up
    close(fd);
    munmap(mappedMemory, size);
    shm_unlink(mapName);

    return 0;
}
