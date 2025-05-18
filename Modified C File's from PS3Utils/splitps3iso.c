#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <dirent.h>
#include <time.h>

#define SPLIT_SIZE       0xFFFF0000
#define BUFFER_SIZE      0x00010000

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

    char path1[0x420];
    char output_folder[0x420] = {0};
    char split_file[0x420];
    char *buffer;

    fp_split = NULL;
    split_index = 0;

    clock_t t_start, t_finish;

    for(int i = 1; i < argc; i++) {
        if(!strcmp(argv[i], "-h") || !strcmp(argv[i], "--headless")) {
            unattended = 1;
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

    fp_split = fopen(split_file, "wb");
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

    const char* iso_base = path1;
    if (output_folder[0]) {
        iso_base = get_basename(path1);
    }

    if(verbose) printf("Splitting ISO: %s\n", path1);

    do
    {
        len = fread(buffer, 1, BUFFER_SIZE, fp);
        fwrite(buffer, 1, len, fp_split);
        count += len;
        if(verbose) printf("Wrote %u bytes to %s (part %d)\n", len, split_file, split_index);

        if (count == SPLIT_SIZE)
        {
            count = 0;
            fclose(fp_split);
            build_split_file(split_file, sizeof(split_file), output_folder, iso_base, split_index++);
            fp_split = fopen(split_file, "wb");
            if(!fp_split) {
                printf("Error!: Cannot open split file for writing\n\n");
                free(buffer);
                fclose(fp);
                if(!unattended) { printf("Press ENTER key to exit\n"); get_input_char(); }
                return -1;
            }
            if(verbose) printf("Writing to: %s\n", split_file);
        }
    }
    while(len == BUFFER_SIZE);

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