# ON PATCH FIXING:

- Update `patch_constants.php` config
- Nuke everything with nuke_all.sh script + clear_docker_cache.sh script
- Run first_start.sh

# RECOMMENDED CRONS TO SET:
0 0 * * * scripts/cron/store_logs.sh

# TODO:
- Create API documentation
- Create API key system
- Create a container to manage crons(?)
- Make API endpoints instead of the various controllers
- Make function to recreate preaproved items based on rules and store them in DB instead of config
- Separate frontend to another repo
- Make cron and script to backup mariaDB
- Find a way to not have so many scripts in root when working
- Move dockerfiles to docker/dockerfiles
- Update README.md more often
- Ponder fusing nuke_all script with clear_docker_cache script (bat and sh)
- Move this to do list to github issues
- Grafana container(?)
- Autogenerate the patch constants instead of them being kept in config
- Figure out what's Dependabot
- Make worlds DB instead of config
- What is even going on with Item_model -> get_by_name() ??????????
- Check var type conversions on item CSV import (Updateddb.php)
- Make view for Bi-color Gemstone Profit Calculator and working AJAX script
- Give loading divs a fancy gif for UX improvement

# BY THE 12 PLEASE REMEMBER TO MOVE THESE TO ISSUES