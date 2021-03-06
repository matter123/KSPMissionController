Mission
{
    name = ComSat Contract I
    description = We signed a contract to bring a satellite into a very specific orbit.
    repeatable = true
    reward = 60000
    randomized = true

    clientControlled = true
    lifetime = TIME(1y)

    category = SATELLITE, ORBIT

    OrbitGoal
    {
        body = Kerbin

        minApA = RANDOM(100000, 200000)
        maxApA = ADD(minApA, 5000)

        maxEccentricity = 0.001

        minInclination = RANDOM(0, 88)
        maxInclination = ADD(minInclination, 0.5)
    }
}
