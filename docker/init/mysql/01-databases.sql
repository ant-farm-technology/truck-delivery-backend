-- Tạo database riêng cho từng microservice (mỗi service owns DB của mình)
-- Chạy tự động khi MySQL container khởi động lần đầu

CREATE DATABASE IF NOT EXISTS `truck_identity`   CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `truck_order`      CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `truck_driver`     CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `truck_shipment`   CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `truck_tracking`   CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `truck_notification` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `truck_payment`    CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Grant quyền cho app user (tạo bởi MYSQL_USER env)
GRANT ALL PRIVILEGES ON `truck_identity`.*    TO 'truckdelivery'@'%';
GRANT ALL PRIVILEGES ON `truck_order`.*       TO 'truckdelivery'@'%';
GRANT ALL PRIVILEGES ON `truck_driver`.*      TO 'truckdelivery'@'%';
GRANT ALL PRIVILEGES ON `truck_shipment`.*    TO 'truckdelivery'@'%';
GRANT ALL PRIVILEGES ON `truck_tracking`.*    TO 'truckdelivery'@'%';
GRANT ALL PRIVILEGES ON `truck_notification`.* TO 'truckdelivery'@'%';
GRANT ALL PRIVILEGES ON `truck_payment`.*     TO 'truckdelivery'@'%';

FLUSH PRIVILEGES;
