/* Simple test program for Denkovi FTDI relay board */

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
    int retval = 0;
    char input[32];
    int channel;

    printf("=== Denkovi FTDI Relay Board Test ===\n\n");

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
        retval = 1;
        goto done;
    }
    printf("✓ Device opened (result: %d)\n", f);

    /* Reset device */
    ftdi_usb_reset(ftdi);
    ftdi_usb_purge_buffers(ftdi);
    ftdi_set_baudrate(ftdi, 9600);

    /* Enable bitbang mode */
    f = ftdi_set_bitmode(ftdi, 0xFF, BITMODE_BITBANG);
    if (f < 0)
    {
        fprintf(stderr, "ERROR: Failed to set bitbang mode: %d (%s)\n", f, ftdi_get_error_string(ftdi));
        retval = 1;
        goto done;
    }
    printf("✓ Bitbang mode enabled\n");

    /* Read initial state */
    f = ftdi_read_pins(ftdi, &pins);
    printf("✓ Initial pin state: 0x%02X (read result: %d)\n\n", pins, f);

    /* Test: All relays OFF (write 0x00) */
    printf("Setting all relays OFF (0x00)...\n");
    buf[0] = 0x00;
    f = ftdi_write_data(ftdi, buf, 1);
    usleep(100000);
    ftdi_read_pins(ftdi, &pins);
    printf("  Wrote 0x00, read back: 0x%02X\n\n", pins);

    /* Cycle through each relay */
    printf("Testing each relay individually:\n");
    printf("(Press Enter to continue after each, or 'q' to quit)\n\n");

    for (channel = 1; channel <= 8; channel++)
    {
        unsigned char state = 1 << (channel - 1);

        /* Turn ON */
        buf[0] = state;
        f = ftdi_write_data(ftdi, buf, 1);
        usleep(50000);
        ftdi_read_pins(ftdi, &pins);
        printf("Relay %d ON:  wrote 0x%02X, read 0x%02X -- ", channel, state, pins);
        fflush(stdout);

        if (fgets(input, sizeof(input), stdin) != NULL && input[0] == 'q')
            break;

        /* Turn OFF */
        buf[0] = 0x00;
        f = ftdi_write_data(ftdi, buf, 1);
        usleep(50000);
        ftdi_read_pins(ftdi, &pins);
        printf("Relay %d OFF: wrote 0x00, read 0x%02X\n", channel, pins);
    }

    /* All ON test */
    printf("\nAll relays ON (0xFF)...\n");
    buf[0] = 0xFF;
    f = ftdi_write_data(ftdi, buf, 1);
    usleep(100000);
    ftdi_read_pins(ftdi, &pins);
    printf("  Wrote 0xFF, read back: 0x%02X\n", pins);

    printf("Press Enter to turn all off...");
    fgets(input, sizeof(input), stdin);

    /* All OFF */
    printf("All relays OFF (0x00)...\n");
    buf[0] = 0x00;
    f = ftdi_write_data(ftdi, buf, 1);
    usleep(100000);
    ftdi_read_pins(ftdi, &pins);
    printf("  Wrote 0x00, read back: 0x%02X\n", pins);

done:
    /* Cleanup */
    printf("\nCleaning up...\n");
    ftdi_set_bitmode(ftdi, 0x00, 0);  /* Reset bitmode */
    ftdi_usb_close(ftdi);
    ftdi_free(ftdi);
    printf("✓ Done\n");

    return retval;
}
