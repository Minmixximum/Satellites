import json
import random
from datetime import datetime, timedelta, timezone


def extract_epoch_from_tle(tle_line1):
    """
    浠?TLE 绗竴琛屼腑鎻愬彇鍘嗗厓瀛楃涓诧紙绗?9-32瀛楃锛夛紝骞惰浆鎹负 datetime 瀵硅薄銆?
    """
    epoch_str = tle_line1[18:32].strip()
    days = float(epoch_str[2:])  # DDD.DDDDDDDD
    yy = int(epoch_str[:2])
    year = 1900 + yy if yy >= 57 else 2000 + yy
    base = datetime(year, 1, 1, tzinfo=timezone.utc)
    epoch = base + timedelta(days=days - 1)
    return epoch


def generate_tasks(num_tasks, epoch_start, time_window_hours=24):
    """
    鍩轰簬璧峰鏃堕棿鐢熸垚闅忔満浠诲姟銆?
    姣忎釜浠诲姟鏈?arrival_time 鍜?deadline锛坉eadline 鍦?arrival 涔嬪悗闅忔満澧炲姞 1~6 灏忔椂锛夈€?
    鏃堕棿鏍煎紡锛欼SO 8601锛屽 "2024-01-01T00:00:00"
    """
    tasks = []
    start_time = epoch_start
    for i in range(1, num_tasks + 1):
        offset_seconds = random.uniform(0, time_window_hours * 3600)
        arrival = start_time + timedelta(seconds=offset_seconds)
        deadline_offset = random.uniform(3600, 6 * 3600)
        deadline = arrival + timedelta(seconds=deadline_offset)

        tasks.append({
            "id": f"task_{i:03d}",
            "size": random.choice([200, 500, 800, 1000]),
            "priority": random.randint(1, 5),
            "deadline": deadline.isoformat(timespec='seconds'),
            "arrival_time": arrival.isoformat(timespec='seconds')
        })
    return tasks


def generate_scenario(tle_filepath, num_sats, num_tasks, output_file='scenario.json'):
    """
    浠?TLE 鏂囦欢璇诲彇鍗槦鏁版嵁锛岄€夋嫨鍓?num_sats 棰楋紝鐢熸垚浠诲姟锛岃緭鍑?JSON 鍦烘櫙鏂囦欢銆?
    杩斿洖鐢熸垚鐨?JSON 鏂囦欢璺緞銆?
    """
    # 瀵煎叆 TLE 瑙ｆ瀽鍑芥暟锛堟澶勯渶瑕佷粠 tle_fetcher 瀵煎叆锛屼絾涓洪伩鍏嶅惊鐜緷璧栵紝鍦ㄨ皟鐢ㄥ瀵煎叆锛?
    # 涓轰簡妯″潡鐙珛锛岃繖閲屽亣璁惧閮ㄤ細浼犲叆 satellites 鍒楄〃锛涙垨鍔ㄦ€佸鍏ャ€?
    # 鏇寸畝娲佺殑鏂瑰紡锛氳璋冪敤鑰呰礋璐ｄ紶鍏?satellites 鍒楄〃銆?
    # 鎴戜滑鍦ㄦ璁捐涓烘帴鏀?satellites 鍒楄〃鍙傛暟锛岃€屼笉鏄枃浠惰矾寰勩€?
    pass


# 鏇村ソ鐨勮璁★細鍑芥暟鎺ユ敹鍗槦鍒楄〃锛岃€屼笉鏄枃浠惰矾寰?
def generate_scenario_from_satellites(satellites, num_sats, num_tasks, output_file='scenarios/scenario.json'):
    """
    鍙傛暟锛?
        satellites : list of dict锛屾瘡涓寘鍚?name, tle_line1, tle_line2
        num_sats   : int锛岃浣跨敤鐨勫崼鏄熸暟閲忥紙涓嶈秴杩?len(satellites)锛?
        num_tasks  : int锛岀敓鎴愮殑浠诲姟鏁伴噺
        output_file: str锛岃緭鍑虹殑 JSON 鏂囦欢鍚?
    杩斿洖鐢熸垚鐨?JSON 鏂囦欢璺緞銆?
    """
    if num_sats > len(satellites):
        raise ValueError(f"鍗槦鏁伴噺 {num_sats} 瓒呰繃鍙敤鎬绘暟 {len(satellites)}")

    # 閫夊彇鍓?num_sats 棰楀崼鏄?
    selected = satellites[:num_sats]
    sat_list = []
    for idx, sat in enumerate(selected, start=1):
        sat_list.append({
            "id": f"sat_{idx:03d}",
            "name": sat['name'],
            "tle_line1": sat['tle_line1'],
            "tle_line2": sat['tle_line2'],
            "capacity": 1000,
            "storage": 1000
        })

    # 浠庣涓€棰楀崼鏄熺殑 TLE 鎻愬彇鍘嗗厓浣滀负鏃堕棿鍩哄噯
    epoch = extract_epoch_from_tle(selected[0]['tle_line1'])
    tasks = generate_tasks(num_tasks, epoch, time_window_hours=24)

    # 鍥哄畾鍦伴潰绔?
    ground_stations = [
        {
            "id": "gs_001",
            "name": "Ground Station Beijing",
            "latitude": 39.9042,
            "longitude": 116.4074,
            "altitude": 0.044*1000,
            "min_elevation": 10,
            "max_range": 3000
        }
    ]

    scenario = {
        "name": "灏忚妯℃祴璇曞満鏅?,
        "description": f"{num_sats}棰楀崼鏄燂紝1涓湴闈㈢珯锛寋num_tasks}涓换鍔?,
        "satellites": sat_list,
        "ground_stations": ground_stations,
        "tasks": tasks
    }

    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(scenario, f, indent=2, ensure_ascii=False)
    print(f"JSON 閰嶇疆鏂囦欢宸茬敓鎴愶細{output_file}")
    return output_file
