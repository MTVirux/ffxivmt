"""ws_worker server entrypoint: runs the Universalis sales/add consumer."""
from __future__ import annotations

import asyncio

import config
import sales_add
from common import log


def main() -> None:
    log.setup(
        logs_dir=config.LOGS_DIR,
        enabled_channels=[k.lower() for k, v in config.PRINT_TO_LOG.items() if v]
        + ["panic"],
        print_channels=[k.lower() for k, v in config.PRINT_TO_SCREEN.items() if v],
    )
    log.debug("starting ws_worker server")
    asyncio.run(sales_add.start_sales_add())


if __name__ == "__main__":
    main()
