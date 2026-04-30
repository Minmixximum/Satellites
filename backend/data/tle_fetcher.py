import requests
from datetime import datetime, timedelta
try:
    from config import Config
except ImportError:  # pragma: no cover
    from ..config import Config

config = Config()


def tle_epoch_to_date(epoch_str):
    """
    灏?TLE 鍘嗗厓瀛楃涓诧紙濡?'25107.50000000'锛夎浆鎹负 datetime 瀵硅薄銆?
    鏍煎紡锛歒YDDD.DDDDDDDD
    - YY : 骞翠唤鍚庝袱浣嶏紙鍋囧畾涓?20xx 骞达級
    - DDD : 骞寸Н鏃ワ紙1-366锛?
    - 灏忔暟閮ㄥ垎 : 涓€澶╀腑鐨勬椂闂达紙UTC锛?
    """
    if '.' in epoch_str:
        int_part, frac_part = epoch_str.split('.')
    else:
        int_part, frac_part = epoch_str, '0'

    yy = int(int_part[:2])
    doy = int(int_part[2:])
    day_frac = float('0.' + frac_part) if frac_part else 0.0

    year = 2000 + yy
    base_date = datetime(year, 1, 1)
    date = base_date + timedelta(days=doy - 1 + day_frac)
    return date


def fetch_and_save_tle(url=None, output_dir='.'):
    """
    浠?Celestrak 鑾峰彇 last-30-days 鐨?TLE 鏁版嵁锛屽苟浠?TLE 鍘嗗厓鏃ユ湡鍛藉悕淇濆瓨涓?.txt 鏂囦欢銆?
    杩斿洖淇濆瓨鐨勬枃浠惰矾寰勩€?
    """
    if url is None:
        url = config.DATA_SOURCE_URL

    try:
        response = requests.get(url)
        response.raise_for_status()
    except requests.exceptions.RequestException as e:
        print(f"Network request error: {e}")
        return None

    tle_data = response.text.strip()
    lines = tle_data.split('\n')

    if len(lines) < 3:
        print("Not enough TLE data to parse.")
        return None

    # 鎻愬彇绗竴涓崼鏄熺殑鍘嗗厓
    first_tle_line1 = lines[1].strip()
    epoch_str = first_tle_line1[18:32].strip()
    try:
        epoch_date = tle_epoch_to_date(epoch_str)
        date_str = epoch_date.strftime('%Y-%m-%d')
    except Exception as e:
        print(f"Date parsing failed: {e}, using current date")
        date_str = datetime.now().strftime('%Y-%m-%d')

    # filename = f"{output_dir}/tle_{date_str}.txt".replace('//', '/')
    filename = f"{output_dir}/tle.txt".replace('//', '/')
    print(date_str)
    with open(filename, 'w', encoding='utf-8') as f:
        # 鍐欏叆鍘熷 TLE锛堟瘡涓夎涓€缁勶紝鏃犵┖琛岋級
        for i in range(0, len(lines), 3):
            sat_name = lines[i].strip()
            tle_line1 = lines[i + 1].strip()
            tle_line2 = lines[i + 2].strip()
            f.write(f"{sat_name}\n{tle_line1}\n{tle_line2}\n")

    print(f"New TLE data saved to: {filename}")
    return filename


def load_tle_from_file(filepath):
    """
    浠庡凡鏈夌殑 TLE 鏂囨湰鏂囦欢鍔犺浇鍗槦鏁版嵁銆?
    杩斿洖鍗槦鍒楄〃锛屾瘡涓厓绱犱负 {'name':, 'tle_line1':, 'tle_line2':}
    """
    satellites = []
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = [line.strip() for line in f if line.strip() != '']
    for i in range(0, len(lines), 3):
        if i + 2 >= len(lines):
            break
        satellites.append({
            'name': lines[i],
            'tle_line1': lines[i + 1],
            'tle_line2': lines[i + 2]
        })
    return satellites

