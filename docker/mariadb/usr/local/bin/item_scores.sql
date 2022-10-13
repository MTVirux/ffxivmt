-- phpMyAdmin SQL Dump
-- version 5.2.0
-- https://www.phpmyadmin.net/
--
-- Host: ffmt_mariadb
-- Generation Time: Oct 13, 2022 at 10:19 AM
-- Server version: 10.9.3-MariaDB-1:10.9.3+maria~ubu2204
-- PHP Version: 8.0.19

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `ffxiv_db`
--

-- --------------------------------------------------------

--
-- Table structure for table `item_scores`
--

CREATE TABLE `item_scores` (
  `entry_id` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  `name` varchar(255) NOT NULL,
  `world` varchar(255) NOT NULL,
  `datacenter` varchar(255) NOT NULL,
  `region` varchar(255) NOT NULL,
  `craft` tinyint(1) NOT NULL,
  `updated_at` datetime NOT NULL,
  `latest_sale` datetime NOT NULL,
  `5min` float UNSIGNED NOT NULL,
  `15min` float NOT NULL,
  `30min` float NOT NULL,
  `1h` float UNSIGNED NOT NULL,
  `2h` float UNSIGNED NOT NULL,
  `6h` float UNSIGNED NOT NULL,
  `12h` float UNSIGNED NOT NULL,
  `1d` float UNSIGNED NOT NULL,
  `2d` float UNSIGNED NOT NULL,
  `5d` float UNSIGNED NOT NULL,
  `1w` float UNSIGNED NOT NULL,
  `2w` float UNSIGNED NOT NULL,
  `1mo` float UNSIGNED NOT NULL,
  `2mo` float UNSIGNED NOT NULL,
  `6mo` float UNSIGNED NOT NULL,
  `patch` float UNSIGNED NOT NULL,
  `1y` float UNSIGNED NOT NULL,
  `expansion` float UNSIGNED NOT NULL,
  `alltime` float UNSIGNED NOT NULL,
  `craftComplexityWeightUsed` float UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

--
-- Indexes for dumped tables
--

--
-- Indexes for table `item_scores`
--
ALTER TABLE `item_scores`
  ADD PRIMARY KEY (`entry_id`),
  ADD UNIQUE KEY `entry_id` (`entry_id`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `item_scores`
--
ALTER TABLE `item_scores`
  MODIFY `entry_id` int(11) NOT NULL AUTO_INCREMENT;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
