#include "musia.h"
#include "config.h"
#include "ps2_poller.h"
#include "ps2_state.h"
#include "usb_joystick.h"
#include "spi.h"

#include <stm32f0xx_hal.h>
#include "iwdg.h"
#include "usart.h"

config cfg;
spi_sniffer sniffer(&hspi1, &hspi2);
ps2_state ps2State;
ps2_poller poller;
usb_joystick usbJoy(&ps2State);


EXTERNC int musia_main(void) {
#ifndef FAST_SEMIHOSTING_PROFILER_DRIVER
	RetargetInit(&huart1);
#endif

	hspi1.Instance = SPI1;
	hspi2.Instance = SPI2;


	sys_printf("MUSIA started\n");
	HAL_GPIO_WritePin(EE_CS_GPIO_Port, EE_CS_Pin, GPIO_PIN_SET);   // SET = disabled
	
	if (cfg.mode == musia_mode::sniffer)
		sniffer.configure();
	else
		poller.configure();
	
	for(;;) {
		HAL_IWDG_Refresh(&hiwdg);
		
		if (cfg.mode == musia_mode::sniffer) {
			sniffer.work();
			auto* upd = sniffer.getNewPacket();			
			if (upd != nullptr) {
				ps2State.update(upd->cmd, upd->data, upd->pktLength);
				upd->isNew = false;
				usbJoy.sendReport();
			}
		}
		else {
			poller.work();
		 }
	}
}
