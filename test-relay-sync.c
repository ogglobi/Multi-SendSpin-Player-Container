/* Test Denkovi relay with SYNCHRONOUS Bit-Bang mode */

#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <ftdi.h>

/* Bit-bang modes */
#define BITMODE_RESET     0x00
#define BITMODE_BITBANG   0x01  /* Async */
#define BITMODE_SYNCBB    0x04  /* Synchronous - required by Denkovi! */

int main(int argc, char **argv)
{
    struct ftdi_context *ftdi;
    int f;
    unsigned char buf[1];
    unsigned char pins;
    int channel;
    int mode = BITMODE_SYNCBB;  /* Try sync mode first */

    if (argc > 1 && argv[1][0] == 'a') {
        mode = BITMODE_BITBANG;
        printf("Using ASYNC Bit-Bang mode (0x01)\n");
    } else {
        printf("Using SYNC Bit-Bang mode (0x04) - Denkovi recommended\n");
    }

    printf("\n=== Denkovi DAE-CB/Ro8-USB Test ===\n\n");

    if ((ftdi = ftdi_new()) == 0) {
        fprintf(stderr, "ftdi_new failed\n");
        return 1;
    }
    printf("✓ Context created\n");

    f = ftdi_usb_open(ftdi, 0x0403, 0x6001);
    if (f < 0) {
        fprintf(stderr, "ERROR: Unable to open: %d (%s)\n", f, ftdi_get_error_string(ftdi));
        ftdi_free(ftdi);
        return 1;
    }
    printf("✓ Device opened\n");

    /* Full reset sequence */
    printf("Resetting device...\n");
    ftdi_usb_reset(ftdi);
    usleep(100000);

    /* Set latency timer (important for sync mode) */
    f = ftdi_set_latency_timer(ftdi, 2);
    printf("  Latency timer: %s\n", f >= 0 ? "OK" : "failed");

    /* Purge buffers */
    ftdi_tcioflush(ftdi);

    /* Set baud rate - affects bit-bang clock */
    f = ftdi_set_baudrate(ftdi, 9600);
    printf("  Baud rate: %s\n", f >= 0 ? "OK" : "failed");

    /* Enable bit-bang mode */
    f = ftdi_set_bitmode(ftdi, 0xFF, mode);
    if (f < 0) {
        fprintf(stderr, "ERROR: Failed to set bitmode: %d (%s)\n", f, ftdi_get_error_string(ftdi));
        ftdi_usb_close(ftdi);
        ftdi_free(ftdi);
        return 1;
    }
    printf("✓ Bit-bang mode 0x%02X enabled\n\n", mode);

    /* Initial state - all OFF */
    printf("All relays OFF...\n");
    buf[0] = 0x00;
    f = ftdi_write_data(ftdi, buf, 1);
    printf("  Write result: %d\n", f);
    usleep(100000);
    ftdi_read_pins(ftdi, &pins);
    printf("  Read pins: 0x%02X\n\n", pins);

    /* Test each relay - hold for 3 seconds */
    printf("Testing each relay (3 seconds each)...\n");
    printf("Watch for LED and listen for click!\n\n");

    for (channel = 1; channel <= 8; channel++) {
        unsigned char state = 1 << (channel - 1);

        printf("Relay %d ON (0x%02X)...", channel, state);
        fflush(stdout);

        buf[0] = state;
        f = ftdi_write_data(ftdi, buf, 1);

        /* For sync mode, we may need to read back */
        if (mode == BITMODE_SYNCBB) {
            unsigned char dummy;
            ftdi_read_data(ftdi, &dummy, 1);
        }

        usleep(50000);
        ftdi_read_pins(ftdi, &pins);
        printf(" wrote:%d read:0x%02X", f, pins);
        fflush(stdout);

        sleep(3);

        /* Turn off */
        buf[0] = 0x00;
        ftdi_write_data(ftdi, buf, 1);
        if (mode == BITMODE_SYNCBB) {
            unsigned char dummy;
            ftdi_read_data(ftdi, &dummy, 1);
        }
        printf(" -> OFF\n");

        usleep(500000);
    }

    /* All ON */
    printf("\nAll relays ON for 5 seconds...\n");
    buf[0] = 0xFF;
    ftdi_write_data(ftdi, buf, 1);
    if (mode == BITMODE_SYNCBB) {
        unsigned char dummy;
        ftdi_read_data(ftdi, &dummy, 1);
    }
    ftdi_read_pins(ftdi, &pins);
    printf("  Read: 0x%02X\n", pins);
    sleep(5);

    /* All OFF */
    printf("All relays OFF...\n");
    buf[0] = 0x00;
    ftdi_write_data(ftdi, buf, 1);

    /* Cleanup */
    ftdi_set_bitmode(ftdi, 0x00, BITMODE_RESET);
    ftdi_usb_close(ftdi);
    ftdi_free(ftdi);
    printf("\n✓ Done\n");

    return 0;
}
