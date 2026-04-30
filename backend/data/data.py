import sys
import os
# 娣诲姞backend鐩綍鐨勭埗鐩綍锛屼娇backend鍙綔涓哄寘瀵煎叆
backend_parent = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(backend_parent))

from tle_fetcher import fetch_and_save_tle, load_tle_from_file
from scene_generator import generate_scenario_from_satellites
from backend.config import Config

config = Config()
def_sats,def_tasks = config.getDefaultNums()

def main():
    # 1. 涓嬭浇 TLE
    tle_file = fetch_and_save_tle(output_dir='tle')
    if tle_file is None:
        return

    # 2. 鍔犺浇鍗槦
    satellites = load_tle_from_file(tle_file)
    total_sats = len(satellites)
    print(f"Total satellites: {total_sats}")

    # 3. 浣跨敤鍓?5 棰楋紙鑻ヤ笉瓒冲垯鍏ㄩ儴浣跨敤锛?
    num_sats = min(def_sats, total_sats)
    num_tasks = def_tasks

    # 4. 鐢熸垚鍦烘櫙
    generate_scenario_from_satellites(satellites, num_sats, num_tasks, "scenarios/scenario.json")


if __name__ == "__main__":
    main()
