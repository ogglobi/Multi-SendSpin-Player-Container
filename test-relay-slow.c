/* Slow test program for Denkovi FTDI relay board - holds states longer */

#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <string.h>
#include <ftdi.h>

int main(int argc, char **argv)
{
    struct ftdi_context *ftdi;
    int f;
    unsigned char buf[1];
    unsigned char pins;
    int channel;

    printf("=== Denkovi FTDI Relay Board - SLOW Test ===\n\n");

    if ((ftdi = ftdi_new()) == 0)
    {
        fprintf(stderr, "ftdi_new failed\n");
        return EXIT_FAILURE;
    }
    printf("✓ Context created\n");

    f = ftdi_usb_open(ftdi, 0x0403, 0x6001);
    if (f < 0 && f != -5)
    {
        fprintf(stderr, "ERROR: Unable to open device: %d (%s)\n", f, ftdi_get_error_string(ftdi));
        ftdi_free(ftdi);
        return 1;
    }
    printf("✓ Device opened\n");

    /* Reset device */
    ftdi_usb_reset(ftdi);
    ftdi_set_baudrate(ftdi, 9600);

    /* Enable bitbang mode */
    f = ftdi_set_bitmode(ftdi, 0xFF, BITMODE_BITBANG);
    if (f < 0)
    {
        fprintf(stderr, "ERROR: Failed to set bitbang mode: %d\n", f);
        ftdi_usb_close(ftdi);
        ftdi_free(ftdi);
        return 1;
    }
    printf("✓ Bitbang mode enabled\n\n");

    /* Start with all OFF */
    buf[0] = 0x00;
    ftdi_write_data(ftdi, buf, 1);
    sleep(1);

    printf("=== Testing each relay for 2 seconds each ===\n\n");

    for (channel = 1; channel <= 8; channel++)
    {
        unsigned char state = 1 << (channel - 1);

        printf("Relay %d ON (0x%02X)... ", channel, state);
        fflush(stdout);

        buf[0] = state;
        ftdi_write_data(ftdi, buf, 1);

        /* Read back */
        usleep(10000);
        ftdi_read_pins(ftdi, &pins);
        printf("read: 0x%02X", pins);
        fflush(stdout);

        /* Hold for 2 seconds */
        sleep(2);

        /* Turn OFF */
        buf[0] = 0x00;
        ftdi_write_data(ftdi, buf, 1);
        printf(" -> OFF\n");

        usleep(500000); /* 0.5s pause between relays */
    }

    printf("\n=== All relays ON for 5 seconds ===\n");
    buf[0] = 0xFF;
    ftdi_write_data(ftdi, buf, 1);
    ftdi_read_pins(ftdi, &pins);
    printf("Wrote 0xFF, read: 0x%02X\n", pins);
    printf("Watch for all 8 LEDs...\n");
    sleep(5);

    printf("\n=== All relays OFF ===\n");
    buf[0] = 0x00;
    ftdi_write_data(ftdi, buf, 1);
    ftdi_read_pins(ftdi, &pins);
    printf("Wrote 0x00, read: 0x%02X\n", pins);

    /* Cleanup */
    ftdi_set_bitmode(ftdi, 0x00, 0);
    ftdi_usb_close(ftdi);
    ftdi_free(ftdi);
    printf("\n✓ Done\n");

    return 0;
}
