#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <dirent.h>
#include <time.h>
#include <math.h>

#define SPLIT_SIZE       0xFFFF0000
#define BUFFER_SIZE      0x00010000
#define MB_SIZE          (1024 * 1024)

int verbose = 1;
int unattended = 0;

#if defined (_WIN32)
#define stat _stati64
#endif

#if !defined(fseeko64)
#define fseeko64 fseek
#endif

#define u8 unsigned char
#define u16 unsigned short
#define u32 unsigned int
#define u64 unsigned long long

static int get_input_char()
{
    char c = getchar();
    char c2 = c;
    while (c != '\n' && c != EOF)
         c = getchar();
    return c2;
}

static void fixpath(char *p)
{
    u8 * pp = (u8 *) p;
    if(*p == '"') {
        p[strlen(p) -1] = 0;
        memcpy(p, p + 1, strlen(p));
    }
    #ifdef __CYGWIN__
    if(p[0]!=0 && p[1] == ':') {
        p[1] = p[0];
        memmove(p + 9, p, strlen(p) + 1);
        memcpy(p, "/cygdrive/", 10);
    }
    #endif
    while(*pp) {
        if(*pp == '"') {*pp = 0; break;}
        else if(*pp == '\\') *pp = '/';
        else if(*pp > 0 && *pp < 32) {*pp = 0; break;}
        pp++;
    }
}

static FILE *fp_split = NULL;
static int split_index = 0;

// Progress struct for Python polling
struct SplitProgress {
    u64 processed_mb;
    u64 total_mb;
    double percent;
    int eta_hours, eta_mins, eta_secs;
    int part_number;
    char part_filename[0x420];
    double current_speed; // MB/s
};
static struct SplitProgress split_progress;
static int use_gdata = 0;

#ifdef _WIN32
__declspec(dllexport)
#endif
struct SplitProgress* get_split_progress() {
    return &split_progress;
}

static const char* get_basename(const char* path) {
    const char* base = strrchr(path, '/');
#ifdef _WIN32
    if (!base) base = strrchr(path, '\\');
#endif
    return base ? base + 1 : path;
}

static void build_split_file(char* out, size_t outsz, const char* output_folder, const char* iso_base, int idx) {
    if (output_folder && output_folder[0]) {
        size_t len = strlen(output_folder);
        char folder[0x420];
        strncpy(folder, output_folder, sizeof(folder));
        folder[sizeof(folder)-1] = 0;
        if (len > 0 && (folder[len-1] == '/' || folder[len-1] == '\\')) {
            folder[len-1] = 0;
        }
        snprintf(out, outsz, "%s/%s.%d", folder, iso_base, idx);
    } else {
        snprintf(out, outsz, "%s.%d", iso_base, idx);
    }
}

#ifdef _WIN32
__declspec(dllexport)
#endif
int splitps3iso_entry(int argc, const char* argv[])
{
    struct stat s;
    int n, len = 0;
    u32 count = 0;
    int part_number = 1;
    char part_filename[0x420];

    char path1[0x420];
    char output_folder[0x420] = {0};
    char split_file[0x420];
    char *buffer;

    fp_split = NULL;
    split_index = 0;

    clock_t t_start, t_finish;

    // Parse -gdata flag
    for(int i = 1; i < argc; i++) {
        if(!strcmp(argv[i], "-h") || !strcmp(argv[i], "--headless")) {
            unattended = 1;
        }
        if(!strcmp(argv[i], "-gdata")) {
            use_gdata = 1;
        }
    }

    if(sizeof(s.st_size) != 8) {
        printf("Error!: stat st_size must be a 64 bit number!  (size %lu)\n\nPress ENTER key to exit\n\n", sizeof(s.st_size));
        if(!unattended) get_input_char();
        return -1;
    }

    if(argc > 1 && (!strcmp(argv[1], "/?") || !strcmp(argv[1], "--help"))) {
        printf("\nSPLITPS3ISO (c) 2021, Bucanero\n\n");
        printf("%s", "Usage:\n\n"
               "    splitps3iso                                 -> input data from the program\n"
               "    splitps3iso <ISO file>                      -> split ISO image (4Gb)\n"
               "    splitps3iso <ISO file> <output folder>      -> split ISO image to output folder\n"
               "    splitps3iso -h                              -> unattended/headless mode\n");
        return 0;
    }

    if(verbose) printf("\nSPLITPS3ISO (c) 2021, Bucanero\n\n");

    if(argc == 1 && !unattended) {
        printf("Enter PS3 ISO to split:\n");
        if(fgets(path1, 0x420, stdin)==0) {
            printf("Error Input PS3 ISO!\n\nPress ENTER key to exit\n");
            if(!unattended) get_input_char();
            return -1;
        }
        printf("\n");
        path1[strcspn(path1, "\r\n")] = 0;
    } else {
        if(argc >= 2) strcpy(path1, argv[1]); else path1[0] = 0;
        if(argc >= 3) strcpy(output_folder, argv[2]);
        else output_folder[0] = 0;
    }

    if(path1[0] == 0) {
         printf("Error: ISO file doesn't exist!\n\n");
         if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
         return -1;
    }

    fixpath(path1);
    n = strlen(path1);

    if(n >= 4 && (!strcmp(&path1[n - 4], ".iso") || !strcmp(&path1[n - 4], ".ISO"))) {
        const char* iso_base = path1;
        if (output_folder[0]) {
            iso_base = get_basename(path1);
        }
        build_split_file(split_file, sizeof(split_file), output_folder, iso_base, split_index++);
        if(stat(path1, &s)<0) {
            printf("Error: ISO file doesn't exist!\n\n");
            if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
            return -1;
        }
    } else {
        printf("Error: file must have .iso, .ISO extension\n\n");
        if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
        return -1;
    }

    printf("\n");

    FILE *fp = fopen(path1, "rb+");
    if(!fp) {
        printf("Error!: Cannot open ISO file\n\n");
        if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
        return -1;
    }

    t_start = clock();

    // Determine output file name
    const char* iso_base = path1;
    char output_path[0x420];
    if (output_folder[0]) {
        iso_base = get_basename(path1);
        snprintf(output_path, sizeof(output_path), "%s/%s", output_folder, iso_base);
    } else {
        snprintf(output_path, sizeof(output_path), "%s", iso_base);
    }

    // Open first output file
    snprintf(part_filename, sizeof(part_filename), "%s.%d", output_path, part_number-1);
    fp_split = fopen(part_filename, "wb");
    if(!fp_split) {
        printf("Error!: Cannot open split file for writing\n\n");
        if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
        fclose(fp);
        return -1;
    }
    buffer = malloc(BUFFER_SIZE);
    if (!buffer) {
        printf("Error!: Cannot allocate buffer\n\n");
        fclose(fp);
        fclose(fp_split);
        if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
        return -1;
    }

    // Get total file size for progress reporting
    u64 total_size = (u64)s.st_size;
    u64 total_size_mb = total_size / MB_SIZE;
    u64 processed_bytes = 0;
    snprintf(part_filename, sizeof(part_filename), "%s%d", output_path, part_number-1);
    if(verbose && !use_gdata) printf("Splitting ISO: %s (Total size: %llu MB)\n", path1, total_size_mb);
    clock_t progress_timer = clock();
    clock_t last_update = clock();
    double elapsed_seconds = 0;
    double bytes_per_second = 0;
    double eta_seconds = 0;
    u64 part_size_mb = SPLIT_SIZE / MB_SIZE;
    // Variables for current speed calculation
    u64 last_processed_bytes = 0;
    clock_t last_speed_time = t_start;
    double current_speed = 0;
    do
    {
        len = fread(buffer, 1, BUFFER_SIZE, fp);
        fwrite(buffer, 1, len, fp_split);
        count += len;
        processed_bytes += len;
        // Update progress every 0.5 seconds
        clock_t current_time = clock();
        if ((current_time - last_update) > (CLOCKS_PER_SEC / 2)) {
            // Calculate current speed (MB/s) based on bytes processed since last update
            double interval = (double)(current_time - last_speed_time) / CLOCKS_PER_SEC;
            if (interval > 0.01) {
                current_speed = (double)(processed_bytes - last_processed_bytes) / interval;
            }
            last_processed_bytes = processed_bytes;
            last_speed_time = current_time;
            last_update = current_time;
            // Calculate progress and ETA
            elapsed_seconds = (double)(current_time - t_start) / CLOCKS_PER_SEC;
            bytes_per_second = current_speed;
            eta_seconds = (total_size - processed_bytes) / (bytes_per_second > 1 ? bytes_per_second : 1);
            int eta_hours = (int)(eta_seconds / 3600);
            int eta_mins = (int)((eta_seconds - (eta_hours * 3600)) / 60);
            int eta_secs = (int)(eta_seconds - (eta_hours * 3600) - (eta_mins * 60));
            u32 current_mb = count / MB_SIZE;
            u64 total_processed_mb = processed_bytes / MB_SIZE;
            double percent_complete = (double)processed_bytes * 100.0 / (double)total_size;
            snprintf(part_filename, sizeof(part_filename), "%s.%d", output_path, part_number-1);
            if(use_gdata) {
                split_progress.processed_mb = total_processed_mb;
                split_progress.total_mb = total_size_mb;
                split_progress.percent = percent_complete;
                split_progress.eta_hours = eta_hours;
                split_progress.eta_mins = eta_mins;
                split_progress.eta_secs = eta_secs;
                split_progress.part_number = part_number;
                strncpy(split_progress.part_filename, part_filename, sizeof(split_progress.part_filename)-1);
                split_progress.part_filename[sizeof(split_progress.part_filename)-1] = 0;
                split_progress.current_speed = current_speed / MB_SIZE; // Set current speed in MB/s
                // Optionally add current_speed to struct if needed
            } else if(verbose) {
                printf("\rSplitting Part %d (%s): %llu/%llu MB (%.2f%%) at %.2f MB/s - ETA: %02d:%02d:%02d", 
                   part_number, part_filename, total_processed_mb, total_size_mb, percent_complete,
                   current_speed / MB_SIZE, eta_hours, eta_mins, eta_secs);
                fflush(stdout);
            }
        }
        if (count >= SPLIT_SIZE) {
            if (verbose && !use_gdata) printf("\nCompleted part %d (%u MB)\n", part_number, count / MB_SIZE);
            fclose(fp_split);
            part_number++;
            count = 0;
            snprintf(part_filename, sizeof(part_filename), "%s.%d", output_path, part_number-1);
            fp_split = fopen(part_filename, "wb");
            if(!fp_split) {
                printf("\nError!: Cannot open split file for writing\n\n");
                free(buffer);
                fclose(fp);
                if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
                return -1;
            }
            if(verbose && !use_gdata) printf("Writing to: %s\n", part_filename);
        }
    }
    while(len == BUFFER_SIZE);
    
    // Print final progress
    if (verbose) {
        u64 final_mb = processed_bytes / MB_SIZE;
        printf("\nCompleted all parts: %llu MB total\n", final_mb);
    }

    free(buffer);

    if(fp) fclose(fp);
    if(fp_split) {fclose(fp_split); fp_split = NULL;}

    t_finish = clock();

    if(verbose) printf("Finish!\n\n");
    if(verbose) printf("Total Time (HH:MM:SS): %2.2u:%2.2u:%2.2u.%u\n\n", (u32) ((t_finish - t_start)/(CLOCKS_PER_SEC * 3600)),
        (u32) (((t_finish - t_start)/(CLOCKS_PER_SEC * 60)) % 60), (u32) (((t_finish - t_start)/(CLOCKS_PER_SEC)) % 60),
        (u32) (((t_finish - t_start)/(CLOCKS_PER_SEC/100)) % 100));

    if(argc < 2 && !unattended) {
        printf("\nPress ENTER key to exit\n");
        get_input_char();
    }

    return 0;
}